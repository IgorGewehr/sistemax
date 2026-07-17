using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

public sealed class SqliteFatoReceitaDiariaRepositoryContractTests : FatoReceitaDiariaRepositoryContractTests, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-fato-receita-contract-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;

    public SqliteFatoReceitaDiariaRepositoryContractTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        new FinanceiroSchemaMigrationV8().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        new FinanceiroSchemaMigrationV19().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        transaction.Commit();
    }

    protected override IFatoReceitaDiariaRepository CriarRepositorio()
        => new SqliteFatoReceitaDiariaRepository(_connectionFactory, new SessaoSempreInativa());

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
}
