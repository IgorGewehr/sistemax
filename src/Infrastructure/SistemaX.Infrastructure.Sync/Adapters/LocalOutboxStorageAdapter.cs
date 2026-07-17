using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Adapters;

/// <inheritdoc cref="ISyncStorageAdapter"/>
public sealed class LocalOutboxStorageAdapter(IOutboxStore outboxStore, ILocalSqliteConnectionFactory connectionFactory) : ISyncStorageAdapter
{
    public Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int maxBatchSize, CancellationToken ct = default)
        => outboxStore.GetPendingBatchAsync(maxBatchSize, ct);

    public Task MarkConfirmedAsync(IEnumerable<string> ids, CancellationToken ct = default)
        => outboxStore.MarkConfirmedAsync(ids, ct);

    public Task MarkFailedAsync(string id, string error, TimeSpan nextAttemptDelay, CancellationToken ct = default)
        => outboxStore.MarkFailedAsync(id, error, nextAttemptDelay, ct);

    public Task MoveToDeadLetterAsync(string id, CancellationToken ct = default)
        => outboxStore.MoveToDeadLetterAsync(id, ct);

    public Task<int> CountPendingAsync(CancellationToken ct = default)
        => outboxStore.CountPendingAsync(ct);

    public async Task<SyncCursor> GetCursorAsync(CancellationToken ct = default)
    {
        await EnsureCursorTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT server_sequence FROM sync_cursor WHERE id = 1;";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return new SyncCursor(result is null ? 0 : Convert.ToInt64(result));
    }

    public async Task SaveCursorAsync(SyncCursor cursor, CancellationToken ct = default)
    {
        await EnsureCursorTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO sync_cursor (id, server_sequence) VALUES (1, $seq)
            ON CONFLICT(id) DO UPDATE SET server_sequence = excluded.server_sequence;
            """;
        cmd.Parameters.AddWithValue("$seq", cursor.ServerSequence);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureCursorTableAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS sync_cursor (id INTEGER PRIMARY KEY CHECK (id = 1), server_sequence INTEGER NOT NULL);";
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
