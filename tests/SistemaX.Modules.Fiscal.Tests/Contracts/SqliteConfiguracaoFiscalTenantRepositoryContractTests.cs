using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

public sealed class SqliteConfiguracaoFiscalTenantRepositoryContractTests : ConfiguracaoFiscalTenantRepositoryContractTests, IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"sistemax-configuracao-fiscal-tenant-contract-{Guid.NewGuid():N}.db");
    private readonly LocalSqliteConnectionFactory _connectionFactory;

    public SqliteConfiguracaoFiscalTenantRepositoryContractTests()
    {
        _connectionFactory = new LocalSqliteConnectionFactory(Options.Create(new LocalDatabaseOptions { DatabasePath = _dbPath }));

        using var connection = _connectionFactory.OpenConnection();
        using var transaction = connection.BeginTransaction();
        new FiscalSchemaMigrationV1().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        // V3 adiciona csc_id/csc_token em fiscal_configuracoes_tenant (Passo 3b) — sem ela o
        // SELECT/INSERT de SqliteConfiguracaoFiscalTenantRepository quebra ("no column named csc_id").
        new FiscalSchemaMigrationV3().AplicarAsync(connection, transaction, CancellationToken.None).GetAwaiter().GetResult();
        transaction.Commit();
    }

    protected override IConfiguracaoFiscalTenantRepository CriarRepositorio()
        => new SqliteConfiguracaoFiscalTenantRepository(_connectionFactory, new SessaoSempreInativa());

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
