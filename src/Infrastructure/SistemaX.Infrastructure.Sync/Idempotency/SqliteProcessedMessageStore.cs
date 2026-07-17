using SistemaX.Infrastructure.Local;

namespace SistemaX.Infrastructure.Sync.Idempotency;

/// <inheritdoc cref="IProcessedMessageStore"/>
public sealed class SqliteProcessedMessageStore(ILocalSqliteConnectionFactory connectionFactory) : IProcessedMessageStore
{
    public async Task<bool> WasProcessedAsync(string id, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM processed_messages WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is not null;
    }

    public async Task MarkProcessedAsync(string id, string entityType, string entityId, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO processed_messages (id, entity_type, entity_id, processed_at_utc)
            VALUES ($id, $entityType, $entityId, $processedAt)
            ON CONFLICT(id) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$entityType", entityType);
        cmd.Parameters.AddWithValue("$entityId", entityId);
        cmd.Parameters.AddWithValue("$processedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS processed_messages (
                id                TEXT PRIMARY KEY,
                entity_type       TEXT NOT NULL,
                entity_id         TEXT NOT NULL,
                processed_at_utc  INTEGER NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
