using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Roda o MESMO contrato do port contra SQLite real — arquivo temporário POR TESTE. Ver
/// <c>SqliteFornecedorRepositoryContractTests</c> (Compras/F0) — mesmo padrão copiado literalmente.
/// </summary>
public sealed class SqliteContaAReceberRepositoryContractTests : ContaAReceberRepositoryContractTests, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-conta-a-receber-contract-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;

    public SqliteContaAReceberRepositoryContractTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        new FinanceiroSchemaMigrationV1().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        new FinanceiroSchemaMigrationV16().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        new FinanceiroSchemaMigrationV21().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        transaction.Commit();
    }

    protected override IContaAReceberRepository CriarRepositorio()
        => new SqliteContaAReceberRepository(_connectionFactory, new SessaoSempreInativa());

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
    /// abre sua própria conexão curta.</summary>
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
