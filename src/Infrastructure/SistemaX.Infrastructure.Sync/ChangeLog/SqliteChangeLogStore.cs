using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.ChangeLog;

/// <inheritdoc cref="IChangeLogStore"/>
public sealed class SqliteChangeLogStore(ILocalSqliteConnectionFactory connectionFactory) : IChangeLogStore
{
    public async Task<long> AppendAsync(IncomingChange change, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO change_log (id, entity_type, entity_id, operation, payload_json, origin_terminal_id, occurred_at_utc)
            VALUES ($id, $entityType, $entityId, $operation, $payloadJson, $originTerminalId, $occurredAt)
            ON CONFLICT(id) DO NOTHING
            RETURNING server_sequence;
            """;
        cmd.Parameters.AddWithValue("$id", change.Id);
        cmd.Parameters.AddWithValue("$entityType", change.EntityType);
        cmd.Parameters.AddWithValue("$entityId", change.EntityId);
        cmd.Parameters.AddWithValue("$operation", change.Operation);
        cmd.Parameters.AddWithValue("$payloadJson", change.PayloadJson);
        cmd.Parameters.AddWithValue("$originTerminalId", change.OriginTerminalId);
        cmd.Parameters.AddWithValue("$occurredAt", change.OccurredAtUtc.ToUnixTimeMilliseconds());

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (result is not null)
        {
            return Convert.ToInt64(result);
        }

        // ON CONFLICT DO NOTHING não retorna linha — a mudança já existia (reenvio idempotente).
        // Busca o sequence já atribuído na tentativa anterior.
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT server_sequence FROM change_log WHERE id = $id;";
        lookup.Parameters.AddWithValue("$id", change.Id);
        return Convert.ToInt64(await lookup.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<RemoteChange>> GetSinceAsync(long sinceServerSequence, string excludeTerminalId, int maxItems, CancellationToken ct = default)
    {
        await EnsureTableAsync(ct).ConfigureAwait(false);

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT server_sequence, id, entity_type, entity_id, operation, payload_json, origin_terminal_id, occurred_at_utc
            FROM change_log
            WHERE server_sequence > $since
              AND origin_terminal_id <> $excludeTerminalId
            ORDER BY server_sequence ASC
            LIMIT $maxItems;
            """;
        cmd.Parameters.AddWithValue("$since", sinceServerSequence);
        cmd.Parameters.AddWithValue("$excludeTerminalId", excludeTerminalId);
        cmd.Parameters.AddWithValue("$maxItems", maxItems);

        var result = new List<RemoteChange>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(new RemoteChange(
                Id: reader.GetString(1),
                EntityType: reader.GetString(2),
                EntityId: reader.GetString(3),
                Operation: reader.GetString(4),
                PayloadJson: reader.GetString(5),
                OriginTerminalId: reader.GetString(6),
                ServerSequence: reader.GetInt64(0),
                OccurredAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7))));
        }

        return result;
    }

    private async Task EnsureTableAsync(CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS change_log (
                server_sequence     INTEGER PRIMARY KEY AUTOINCREMENT,
                id                  TEXT NOT NULL UNIQUE,
                entity_type         TEXT NOT NULL,
                entity_id           TEXT NOT NULL,
                operation           TEXT NOT NULL,
                payload_json        TEXT NOT NULL,
                origin_terminal_id  TEXT NOT NULL,
                occurred_at_utc     INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_change_log_sequence_terminal
                ON change_log (server_sequence, origin_terminal_id);
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
