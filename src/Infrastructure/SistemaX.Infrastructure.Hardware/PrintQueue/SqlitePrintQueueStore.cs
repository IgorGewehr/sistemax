using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Hardware.Devices.Printer;

namespace SistemaX.Infrastructure.Hardware.PrintQueue;

/// <inheritdoc cref="IPrintQueueStore"/>
public sealed class SqlitePrintQueueStore : IPrintQueueStore
{
    private readonly string _connectionString;
    private readonly int _maxAttempts;
    private bool _schemaEnsured;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);

    public SqlitePrintQueueStore(IOptions<HardwareOptions> options)
    {
        var opts = options.Value;
        _maxAttempts = opts.PrintJobMaxAttempts;

        var directory = Path.GetDirectoryName(opts.PrintQueueDatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = opts.PrintQueueDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task<string> EnqueueAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        var id = Guid.NewGuid().ToString("N");
        await using var connection = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO print_queue (id, commands_json, status, attempts, max_attempts, created_at_utc, last_attempt_at_utc, last_error)
            VALUES ($id, $commandsJson, 'Pending', 0, $maxAttempts, $createdAt, NULL, NULL);
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$commandsJson", JsonSerializer.Serialize(commands));
        cmd.Parameters.AddWithValue("$maxAttempts", _maxAttempts);
        cmd.Parameters.AddWithValue("$createdAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return id;
    }

    public async Task<IReadOnlyList<PrintJob>> GetPendingAsync(int maxItems, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        await using var connection = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, commands_json, status, attempts, max_attempts, created_at_utc, last_attempt_at_utc, last_error
            FROM print_queue
            WHERE status = 'Pending'
            ORDER BY created_at_utc ASC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", maxItems);

        var result = new List<PrintJob>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new PrintJob(
                Id: reader.GetString(0),
                CommandsJson: reader.GetString(1),
                Status: Enum.Parse<PrintJobStatus>(reader.GetString(2)),
                Attempts: reader.GetInt32(3),
                MaxAttempts: reader.GetInt32(4),
                CreatedAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                LastAttemptAtUtc: reader.IsDBNull(6) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)),
                LastError: reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return result;
    }

    public async Task MarkCompletedAsync(string id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        await using var connection = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE print_queue SET status = 'Completed', last_attempt_at_utc = $now WHERE id = $id;";
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(string id, string error, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        await using var connection = await OpenAsync(ct).ConfigureAwait(false);

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                """
                UPDATE print_queue
                SET attempts = attempts + 1,
                    last_error = $error,
                    last_attempt_at_utc = $now,
                    status = CASE WHEN attempts + 1 >= max_attempts THEN 'Failed' ELSE 'Pending' END
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$error", error);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<int> CountPendingAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct).ConfigureAwait(false);

        await using var connection = await OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM print_queue WHERE status = 'Pending';";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        return connection;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured)
        {
            return;
        }

        await _schemaGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_schemaEnsured)
            {
                return;
            }

            await using var connection = await OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                CREATE TABLE IF NOT EXISTS print_queue (
                    id                   TEXT PRIMARY KEY,
                    commands_json        TEXT NOT NULL,
                    status               TEXT NOT NULL,
                    attempts             INTEGER NOT NULL DEFAULT 0,
                    max_attempts         INTEGER NOT NULL,
                    created_at_utc       INTEGER NOT NULL,
                    last_attempt_at_utc  INTEGER NULL,
                    last_error           TEXT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_print_queue_status_created
                    ON print_queue (status, created_at_utc);
                """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            _schemaEnsured = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }
}
