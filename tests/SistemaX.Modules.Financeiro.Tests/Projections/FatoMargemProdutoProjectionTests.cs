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
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// Prova ponta-a-ponta da F1 (fato_margem_produto) — mesmo critério de pronto da F0
/// (ProjectionRunnerReprocessabilityTests): ledger → fold → fact table, com o rateio de
/// <c>CustoBaixadoPorVenda</c> entre os itens de <c>VendaItensMovimentados</c> da MESMA venda, e
/// replay do zero produzindo exatamente o mesmo estado.
/// </summary>
public sealed class FatoMargemProdutoProjectionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-fato-margem-projection-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;
    private readonly SqliteIntegrationEventLedgerStore _ledger;
    private readonly SqliteProjectionStateStore _estado;
    private readonly ProjectionRunner _runner;
    private readonly IFatoMargemProdutoRepository _margemRepo;

    public FatoMargemProdutoProjectionTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using (var connection = _connectionFactory.OpenConnection())
        using (var transaction = connection.BeginTransaction())
        {
            new IntegrationEventsSchemaMigration().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            new FinanceiroSchemaMigrationV10().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
            transaction.Commit();
        }

        _ledger = new SqliteIntegrationEventLedgerStore(_connectionFactory);
        _estado = new SqliteProjectionStateStore(_connectionFactory);
        _runner = new ProjectionRunner(_ledger, _estado, new ScopeFactoryNuncaUsadoNesteTeste());
        _margemRepo = new SqliteFatoMargemProdutoRepository(_connectionFactory, new SessaoSempreInativa());
    }

    [Fact]
    public async Task Fold_aloca_custo_proporcional_a_receita_e_reprocessamento_do_zero_bate_igual()
    {
        var tenantId = "tenant-1";
        var ocorridoEm = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(-3));

        var itens = new List<ItemMovimentado>
        {
            new("produto-a", "Produto A", 2_000, 3_00), // 2 un x R$3,00 = R$6,00 -> 600 centavos
            new("produto-b", "Produto B", 1_000, 4_00), // 1 un x R$4,00 = R$4,00 -> 400 centavos
        };

        await PersistirAsync(new VendaItensMovimentados("venda-1", tenantId, itens, ocorridoEm), "venda.itens:venda-1");
        await PersistirAsync(new CustoBaixadoPorVenda("venda-1", tenantId, 500, ocorridoEm), "venda.custo:venda-1");

        var projecao = new FatoMargemProdutoProjection(_margemRepo);
        await _runner.ExecutarUmaAsync(projecao);

        var dia = new DateOnly(2026, 7, 15);
        var produtoA = await _margemRepo.ObterAsync(tenantId, "produto-a", dia);
        var produtoB = await _margemRepo.ObterAsync(tenantId, "produto-b", dia);

        Assert.Equal(600, produtoA!.ReceitaCentavos);
        Assert.Equal(400, produtoB!.ReceitaCentavos);
        // custo total 500, rateado 600:400 (60%/40%) -> 300 e 200
        Assert.Equal(300, produtoA.CustoCentavos);
        Assert.Equal(200, produtoB.CustoCentavos);
        Assert.Equal(500, produtoA.CustoCentavos + produtoB.CustoCentavos); // soma bate exata (sem sobra de rateio)

        await _runner.ReconstruirAsync(projecao);

        var produtoAReconstruido = await _margemRepo.ObterAsync(tenantId, "produto-a", dia);
        var produtoBReconstruido = await _margemRepo.ObterAsync(tenantId, "produto-b", dia);

        Assert.Equal(produtoA.ReceitaCentavos, produtoAReconstruido!.ReceitaCentavos);
        Assert.Equal(produtoA.CustoCentavos, produtoAReconstruido.CustoCentavos);
        Assert.Equal(produtoB.ReceitaCentavos, produtoBReconstruido!.ReceitaCentavos);
        Assert.Equal(produtoB.CustoCentavos, produtoBReconstruido.CustoCentavos);
    }

    [Fact]
    public async Task Venda_de_servico_sem_custo_baixado_fica_so_com_receita()
    {
        var tenantId = "tenant-1";
        var ocorridoEm = DateTimeOffset.UtcNow;
        var itens = new List<ItemMovimentado> { new("servico-1", "Corte de cabelo", 1_000, 50_00) };

        await PersistirAsync(new VendaItensMovimentados("venda-servico", tenantId, itens, ocorridoEm), "venda.itens:venda-servico");
        // nenhum CustoBaixadoPorVenda publicado — serviço não controla estoque.

        var projecao = new FatoMargemProdutoProjection(_margemRepo);
        await _runner.ExecutarUmaAsync(projecao);

        var dia = BucketingTemporalDoTenant(ocorridoEm);
        var fato = await _margemRepo.ObterAsync(tenantId, "servico-1", dia);

        Assert.Equal(5_000, fato!.ReceitaCentavos);
        Assert.Equal(0, fato.CustoCentavos);
        Assert.Equal(5_000, fato.MargemContribuicaoCentavos);
    }

    private static DateOnly BucketingTemporalDoTenant(DateTimeOffset instante)
        => Application.Comum.BucketingTemporalDoTenant.DiaLocal(instante);

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

    private sealed class ScopeFactoryNuncaUsadoNesteTeste : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotSupportedException();
    }
}
