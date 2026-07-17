using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Infrastructure.Sqlite;

namespace SistemaX.Modules.Vendas.Tests.Contracts;

/// <summary>
/// Roda o MESMO contrato do port contra SQLite real — arquivo temporário POR TESTE (xUnit cria
/// uma instância nova desta classe a cada [Fact], então cada teste tem seu próprio banco). NÃO
/// usa <c>:memory:</c>: cada operação do repositório (fora de uma sessão) abre sua própria
/// conexão, e <c>:memory:</c> sem <c>cache=shared</c> criaria um banco NOVO E VAZIO a cada
/// conexão — o teste "salvar então buscar" falharia mesmo estando correto (ver nota do plano de
/// persistência sobre esse exato ponto).
///
/// As migrações são aplicadas chamando <see cref="VendasSchemaMigrationV1"/>/
/// <see cref="VendasSchemaMigrationV2"/> diretamente — o mesmo caminho que o
/// <c>SchemaMigrationRunner</c> usaria em produção — em vez de duplicar o DDL aqui.
/// </summary>
public sealed class SqliteVendaRepositoryContractTests : VendaRepositoryContractTests, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-venda-contract-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;

    public SqliteVendaRepositoryContractTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        new VendasSchemaMigrationV1().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        new VendasSchemaMigrationV2().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        transaction.Commit();
    }

    protected override IVendaRepository CriarRepositorio()
        => new SqliteVendaRepository(_connectionFactory, new SessaoSempreInativa());

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

    /// <summary>Fake mínimo: simula "nenhum caso de uso iniciou sessão" — o repositório sempre
    /// abre sua própria conexão curta. Testar a participação numa transação ambiente real (commit
    /// vs. rollback) é um caso à parte, não duplicado no contrato compartilhado porque o adapter
    /// in-memory não tem noção de sessão/transação.</summary>
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
}
