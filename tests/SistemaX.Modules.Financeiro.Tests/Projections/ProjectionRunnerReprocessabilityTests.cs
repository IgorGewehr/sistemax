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
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// Prova ponta-a-ponta da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md/ADR-0005): ledger → fold → fact table, e o
/// critério de pronto da Fase 0 — "replay do ledger reconstrói a fact table IDÊNTICA ao estado
/// acumulado incrementalmente".
/// </summary>
public sealed class ProjectionRunnerReprocessabilityTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-projection-runner-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;
    private readonly SqliteIntegrationEventLedgerStore _ledger;
    private readonly SqliteProjectionStateStore _estado;
    private readonly ProjectionRunner _runner;
    private readonly IFatoReceitaDiariaRepository _receitaRepo;
    private readonly IFatoCaixaDiarioRepository _caixaRepo;
    private readonly IFatoCustoDiarioRepository _custoRepo;

    public ProjectionRunnerReprocessabilityTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new IntegrationEventsSchemaMigration().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV8().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV9().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV19().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV20().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        _ledger = new SqliteIntegrationEventLedgerStore(_connectionFactory);
        _estado = new SqliteProjectionStateStore(_connectionFactory);
        _runner = new ProjectionRunner(_ledger, _estado, new ScopeFactoryNuncaUsadoNesteTeste());

        var sessao = new SessaoSempreInativa();
        _receitaRepo = new SqliteFatoReceitaDiariaRepository(_connectionFactory, sessao);
        _caixaRepo = new SqliteFatoCaixaDiarioRepository(_connectionFactory, sessao);
        _custoRepo = new SqliteFatoCustoDiarioRepository(_connectionFactory, sessao);
    }

    [Fact]
    public async Task Fold_incremental_e_reconstrucao_do_zero_produzem_exatamente_o_mesmo_estado()
    {
        var tenantId = "tenant-1";
        var ocorridoEm = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(-3));

        await PersistirAsync(new VendaConcluida("v1", tenantId, 10_000, "pix", ocorridoEm), "venda.concluida:v1");
        await PersistirAsync(new VendaConcluida("v2", tenantId, 5_000, "credito", ocorridoEm), "venda.concluida:v2"); // a prazo
        await PersistirAsync(new VendaEstornada("v1", tenantId, 10_000, ocorridoEm.AddHours(1)), "venda.estornada:v1");
        await PersistirAsync(new PedidoPago("p1", tenantId, 2_500, "dinheiro", ocorridoEm.AddHours(2)), "pedido.pago:p1");
        await PersistirAsync(new CustoBaixadoPorVenda("v1", tenantId, 6_000, ocorridoEm), "venda.custo:v1");

        var receitaProjecao = new FatoReceitaDiariaProjection(_receitaRepo);
        var caixaProjecao = new FatoCaixaDiarioProjection(_caixaRepo);
        var custoProjecao = new FatoCustoDiarioProjection(_custoRepo);

        await _runner.ExecutarUmaAsync(receitaProjecao);
        await _runner.ExecutarUmaAsync(caixaProjecao);
        await _runner.ExecutarUmaAsync(custoProjecao);

        var dia = new DateOnly(2026, 7, 15);
        var receitaIncremental = await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio);
        var caixaIncremental = await _caixaRepo.ObterAsync(tenantId, dia);
        var custoIncremental = await _custoRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio);

        // receita: +10000 (v1) +5000 (v2) -10000 (estorno v1) +2500 (pedido) = 7500
        Assert.Equal(7_500, receitaIncremental!.ReceitaCentavos);
        // caixa: entrada v1 (pix, à vista) + entrada pedido; v2 é a prazo, não entra
        Assert.Equal(12_500, caixaIncremental!.EntradasCentavos);
        Assert.Equal(10_000, caixaIncremental.SaidasCentavos); // estorno de v1
        Assert.Equal(2_500, caixaIncremental.SaldoDiaCentavos);
        // CMV real da venda v1 — o gap que fato_custo_diario fecha (ledger tinha o fato, nada foldava).
        Assert.Equal(6_000, custoIncremental!.CustoCentavos);

        // REPROCESSABILIDADE (critério de pronto da F0): DROP + replay tem que bater igualzinho.
        await _runner.ReconstruirAsync(receitaProjecao);
        await _runner.ReconstruirAsync(caixaProjecao);
        await _runner.ReconstruirAsync(custoProjecao);

        var receitaReconstruida = await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio);
        var caixaReconstruido = await _caixaRepo.ObterAsync(tenantId, dia);
        var custoReconstruido = await _custoRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio);

        Assert.Equal(receitaIncremental.ReceitaCentavos, receitaReconstruida!.ReceitaCentavos);
        Assert.Equal(caixaIncremental.EntradasCentavos, caixaReconstruido!.EntradasCentavos);
        Assert.Equal(caixaIncremental.SaidasCentavos, caixaReconstruido.SaidasCentavos);
        Assert.Equal(custoIncremental.CustoCentavos, custoReconstruido!.CustoCentavos);
    }

    /// <summary>
    /// P0-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — "RBT12 deve incluir TODAS as
    /// correntes (hoje não inclui assinaturas)". <c>CobrancaDeAssinaturaGerada</c> é o evento que
    /// fecha esse gap: precisa foldar em <c>fato_receita_diaria</c> na corrente Recorrente, exatamente
    /// como qualquer outro evento de receita já reconhecida.
    /// </summary>
    [Fact]
    public async Task CobrancaDeAssinaturaGerada_folda_em_fato_receita_diaria_na_corrente_recorrente()
    {
        var tenantId = "tenant-assinatura";
        var ocorridoEm = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(-3));

        await PersistirAsync(new CobrancaDeAssinaturaGerada("assinatura-1", tenantId, 34_900, ocorridoEm), "assinatura.cobranca:assinatura-1:202607");

        var receitaProjecao = new FatoReceitaDiariaProjection(_receitaRepo);
        await _runner.ExecutarUmaAsync(receitaProjecao);

        var dia = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        var recorrente = await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Recorrente);
        Assert.Equal(34_900, recorrente!.ReceitaCentavos);

        // reprocessabilidade: DROP + replay bate igual.
        await _runner.ReconstruirAsync(receitaProjecao);
        var recorrenteReconstruido = await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Recorrente);
        Assert.Equal(recorrente.ReceitaCentavos, recorrenteReconstruido!.ReceitaCentavos);
    }

    [Fact]
    public async Task Catchup_e_incremental_nao_reprocessa_o_que_ja_passou_do_cursor()
    {
        var tenantId = "tenant-1";
        var ocorridoEm = DateTimeOffset.UtcNow;
        await PersistirAsync(new VendaConcluida("v1", tenantId, 1_000, "pix", ocorridoEm), "venda.concluida:v1");

        var projecao = new FatoReceitaDiariaProjection(_receitaRepo);
        await _runner.ExecutarUmaAsync(projecao);

        var cursorAposPrimeiraRodada = await _estado.ObterCursorAsync(projecao.Nome);
        Assert.True(cursorAposPrimeiraRodada > 0);

        // roda de novo sem evento novo — cursor estável, valor não duplica
        await _runner.ExecutarUmaAsync(projecao);
        Assert.Equal(cursorAposPrimeiraRodada, await _estado.ObterCursorAsync(projecao.Nome));

        var dia = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        Assert.Equal(1_000, (await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio))!.ReceitaCentavos);

        // evento novo chega — catch-up avança o cursor e soma só o delta novo
        await PersistirAsync(new VendaConcluida("v2", tenantId, 500, "pix", ocorridoEm), "venda.concluida:v2");
        await _runner.ExecutarUmaAsync(projecao);

        Assert.True(await _estado.ObterCursorAsync(projecao.Nome) > cursorAposPrimeiraRodada);
        Assert.Equal(1_500, (await _receitaRepo.ObterAsync(tenantId, dia, CorrenteDeReceita.Comercio))!.ReceitaCentavos);
    }

    private Task PersistirAsync(IIntegrationEvent evento, string chaveIdempotencia)
        => _ledger.AppendAsync(evento.GetType().Name, evento.TenantId, JsonSerializer.Serialize(evento, evento.GetType()), evento.OcorridoEm, chaveIdempotencia);

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

    /// <summary>Este teste só chama <see cref="ProjectionRunner.ExecutarUmaAsync"/>/
    /// <see cref="ProjectionRunner.ReconstruirAsync"/> (que recebem a projeção já instanciada) —
    /// nunca <see cref="ProjectionRunner.ExecutarTudoAsync"/> (o único que usa o scope factory
    /// para resolver <c>IProjection</c> via DI).</summary>
    private sealed class ScopeFactoryNuncaUsadoNesteTeste : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }
}
