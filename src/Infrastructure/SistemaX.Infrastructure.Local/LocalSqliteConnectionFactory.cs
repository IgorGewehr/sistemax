using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace SistemaX.Infrastructure.Local;

/// <inheritdoc cref="ILocalSqliteConnectionFactory"/>
public sealed class LocalSqliteConnectionFactory : ILocalSqliteConnectionFactory
{
    private readonly string _connectionString;

    public LocalSqliteConnectionFactory(IOptions<LocalDatabaseOptions> options)
    {
        var opts = options.Value;
        DatabasePath = opts.DatabasePath;

        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            // Pool = true (default): reduz custo de abrir/fechar conexões de curta duração,
            // sem abrir mão de "cada operação usa sua própria conexão/transação" — a pool só
            // reaproveita o handle nativo por trás.
            Pooling = true
        }.ToString();

        Options = opts;
    }

    private LocalDatabaseOptions Options { get; }

    public string DatabasePath { get; }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        SqlitePragmas.Apply(connection, Options);
        return connection;
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        SqlitePragmas.Apply(connection, Options);
        return connection;
    }
}
