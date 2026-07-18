using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Ledger;
using SistemaX.Infrastructure.Local.Migrations;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.Projections;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Tests.Migrations;

/// <summary>
/// Retrocompatibilidade da migração da dimensão "corrente de receita" (P0-1,
/// docs/financeiro/revisao-domain-fit-cnpj.md): dado gravado ANTES de V16-20 existirem (sem
/// <c>corrente</c>, simulado aqui via INSERT direto — o "estado de produção" antes desta fatia)
/// não pode quebrar nem sumir depois da migração. Duas famílias de tabela, duas estratégias:
/// <list type="bullet">
/// <item><c>contas_a_receber</c>/<c>contas_a_pagar</c> (V16/V17): ALTER + backfill in-place — a
/// linha continua existindo, só ganha a coluna nova inferida do <c>source_ref_modulo</c>/categoria.</item>
/// <item><c>fato_receita_diaria</c>/<c>fato_custo_diario</c> (V19/V20): são fact tables
/// DESCARTÁVEIS por construção — a migração faz DROP+CREATE com <c>corrente</c> na chave e reseta
/// o cursor da projeção; nada quebra porque o dado de verdade é o ledger, e o próximo catch-up
/// refolda tudo com a dimensão nova.</item>
/// </list>
/// </summary>
public sealed class CorrenteDeReceitaMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-corrente-migration-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;

    public CorrenteDeReceitaMigrationTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));
    }

    /// <summary>V16: dado legado de <c>ContaAReceber</c> (venda, OS, assinatura, recorrência
    /// genérica) é retroativamente classificado pela pista mais forte disponível — o
    /// <c>source_ref_modulo</c> materializado na própria linha — sem perder nenhuma outra coluna.</summary>
    [Fact]
    public async Task MigracaoV16_InfereCorrenteDeContasAReceberLegadasPeloSourceRefModuloOuCategoria()
    {
        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new FinanceiroSchemaMigrationV1().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();

            InserirContaAReceberLegada(connection, transaction, "conta-venda", "sale", "servicos");
            InserirContaAReceberLegada(connection, transaction, "conta-os", "appointment", "servicos");
            InserirContaAReceberLegada(connection, transaction, "conta-assinatura", "assinatura", "servicos");
            InserirContaAReceberLegada(connection, transaction, "conta-recorrencia-mrr", "recorrencia", "receita-recorrente");
            InserirContaAReceberLegada(connection, transaction, "conta-sem-pista", "recorrencia", "aluguel");

            transaction.Commit();
        }

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new FinanceiroSchemaMigrationV16().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV21().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV23().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        var repo = new SqliteContaAReceberRepository(_connectionFactory, new SessaoSempreInativa());

        Assert.Equal(CorrenteDeReceita.Comercio, (await repo.ObterPorIdAsync("conta-venda"))!.Corrente);
        Assert.Equal(CorrenteDeReceita.Servico, (await repo.ObterPorIdAsync("conta-os"))!.Corrente);
        Assert.Equal(CorrenteDeReceita.Recorrente, (await repo.ObterPorIdAsync("conta-assinatura"))!.Corrente);
        Assert.Equal(CorrenteDeReceita.Recorrente, (await repo.ObterPorIdAsync("conta-recorrencia-mrr"))!.Corrente);
        Assert.Null((await repo.ObterPorIdAsync("conta-sem-pista"))!.Corrente);

        // Nenhuma outra coluna foi tocada pelo backfill.
        var venda = await repo.ObterPorIdAsync("conta-venda");
        Assert.Equal("servicos", venda!.CategoriaId);
        Assert.Equal(StatusFinanceiro.Aberto, venda.Status);
    }

    /// <summary>V17: espelha V16 para <c>ContaAPagar</c>, mas a única pista disponível é a
    /// categoria (comissão/CMV de fornecedor) — <c>ContaAPagar</c> não tem um <c>source_ref_modulo</c>
    /// tão discriminante quanto venda/OS/assinatura.</summary>
    [Fact]
    public async Task MigracaoV17_InfereCorrenteDeContasAPagarLegadasPelaCategoria()
    {
        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new FinanceiroSchemaMigrationV2().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();

            InserirContaAPagarLegada(connection, transaction, "conta-comissao", "comissoes");
            InserirContaAPagarLegada(connection, transaction, "conta-cmv", "cmv-fornecedor");
            InserirContaAPagarLegada(connection, transaction, "conta-aluguel", "despesa-com-pessoal");

            transaction.Commit();
        }

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new FinanceiroSchemaMigrationV17().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        var repo = new SqliteContaAPagarRepository(_connectionFactory, new SessaoSempreInativa());

        Assert.Equal(CorrenteDeReceita.Servico, (await repo.ObterPorIdAsync("conta-comissao"))!.Corrente);
        Assert.Equal(CorrenteDeReceita.Comercio, (await repo.ObterPorIdAsync("conta-cmv"))!.Corrente);
        Assert.Null((await repo.ObterPorIdAsync("conta-aluguel"))!.Corrente);
    }

    /// <summary>V19: <c>fato_receita_diaria</c> é reconstruída (não migrada in-place) — o dado
    /// "antigo" (acumulado sem corrente) é substituído pelo replay do ledger, que É a fonte de
    /// verdade. Prova ponta-a-ponta: cursor reseta, replay refolda com a corrente certa por TIPO
    /// de evento, nada do ledger se perde.</summary>
    [Fact]
    public async Task MigracaoV19_ResetaFatoReceitaDiariaEForcaReplayCompletoComCorrenteCorreta()
    {
        var tenantId = "tenant-1";
        var ocorridoEm = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        var dia = new DateOnly(2026, 7, 15);

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new IntegrationEventsSchemaMigration().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV8().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        var ledger = new SqliteIntegrationEventLedgerStore(_connectionFactory);
        var estado = new SqliteProjectionStateStore(_connectionFactory);

        // Ledger já tinha eventos ANTES da migração — o cenário real: o negócio já operava.
        await ledger.AppendAsync(
            nameof(VendaConcluida), tenantId, JsonSerializer.Serialize(new VendaConcluida("v1", tenantId, 10_000, "pix", ocorridoEm)),
            ocorridoEm, "venda.concluida:v1");
        await ledger.AppendAsync(
            nameof(OsFaturada), tenantId, JsonSerializer.Serialize(new OsFaturada("os1", tenantId, 30_000, 5_000, ocorridoEm)),
            ocorridoEm, "os.faturada:os1");

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            // Simula o schema V8 (sem corrente) já com dado acumulado ANTES da migração existir —
            // uma projeção que já rodou incrementalmente sobre esses dois eventos.
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText =
                    "INSERT INTO fato_receita_diaria (tenant_id, dia, receita_centavos, atualizado_em_utc) VALUES ($t, $d, $r, $a);";
                cmd.Parameters.AddWithValue("$t", tenantId);
                cmd.Parameters.AddWithValue("$d", dia.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("$r", 45_000L); // 10_000 (venda) + 35_000 (OS, ainda somada como um único número em V8)
                cmd.Parameters.AddWithValue("$a", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        await estado.SalvarCursorAsync("fato_receita_diaria", 2, CancellationToken.None);
        Assert.True(await estado.ObterCursorAsync("fato_receita_diaria") > 0);

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new FinanceiroSchemaMigrationV19().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        // Cursor resetado — a migração força o próximo catch-up a refoldar TUDO do zero.
        Assert.Equal(0, await estado.ObterCursorAsync("fato_receita_diaria"));

        var receitaRepo = new SqliteFatoReceitaDiariaRepository(_connectionFactory, new SessaoSempreInativa());
        var runner = new ProjectionRunner(ledger, estado, new ScopeFactoryNuncaUsadoNesteTeste());
        await runner.ExecutarUmaAsync(new FatoReceitaDiariaProjection(receitaRepo));

        // Replay refoldou com a dimensão nova: venda é Comercio, OS é Servico — nada se perdeu,
        // e agora a receita está corretamente repartida por corrente (não mais um número único).
        Assert.Equal(10_000, (await receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio))!.ReceitaCentavos);
        Assert.Equal(35_000, (await receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Servico))!.ReceitaCentavos);

        var todasAsLinhas = await receitaRepo.ListarAsync(tenantId, dia, dia);
        Assert.Equal(45_000, todasAsLinhas.Sum(f => f.ReceitaCentavos));
    }

    private static void InserirContaAReceberLegada(SqliteConnection connection, SqliteTransaction transaction, string id, string sourceRefModulo, string categoriaId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO contas_a_receber
                (id, business_id, source_ref_modulo, source_ref_id, source_ref_chave, descricao, categoria_id,
                 centro_de_custo_id, data_competencia, valor_total_centavos, valor_total_moeda, status, criado_em, cliente_id)
            VALUES
                ($id, 'business-1', $srModulo, $id, $srChave, 'Conta legada', $categoriaId,
                 NULL, '2026-07-01T00:00:00+00:00', 10000, 'BRL', 0, '2026-07-01T00:00:00+00:00', NULL);
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$srModulo", sourceRefModulo);
        cmd.Parameters.AddWithValue("$srChave", $"{sourceRefModulo}:{id}");
        cmd.Parameters.AddWithValue("$categoriaId", categoriaId);
        cmd.ExecuteNonQuery();
    }

    private static void InserirContaAPagarLegada(SqliteConnection connection, SqliteTransaction transaction, string id, string categoriaId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO contas_a_pagar
                (id, business_id, source_ref_modulo, source_ref_id, source_ref_chave, descricao, categoria_id,
                 centro_de_custo_id, data_competencia, valor_total_centavos, valor_total_moeda, status, criado_em, fornecedor_id)
            VALUES
                ($id, 'business-1', 'teste', $id, $srChave, 'Conta legada', $categoriaId,
                 NULL, '2026-07-01T00:00:00+00:00', 10000, 'BRL', 0, '2026-07-01T00:00:00+00:00', NULL);
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$srChave", $"teste:{id}");
        cmd.Parameters.AddWithValue("$categoriaId", categoriaId);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        using (var poolConnection = _connectionFactory.OpenConnection())
        {
            SqliteConnection.ClearPool(poolConnection);
        }
        File.Delete(_dbPath);
        File.Delete($"{_dbPath}-wal");
        File.Delete($"{_dbPath}-shm");
    }

    private sealed class SessaoSempreInativa : ILocalSessao
    {
        public ILocalUnitOfWork? Atual => null;

        public Task<ILocalUnitOfWork> IniciarAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Este fake de teste não abre sessão — só exercita o caminho sem transação ambiente.");

        public Task EnqueueOutboxAsync(string entityType, string entityId, OutboxOperation operation, object payload, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ScopeFactoryNuncaUsadoNesteTeste : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }
}
