using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Outbox;

/// <inheritdoc cref="IOutboxStore"/>
public sealed class SqliteOutboxStore(ILocalSqliteConnectionFactory connectionFactory) : IOutboxStore
{
    public async Task EnqueueAsync(SqliteConnection connection, SqliteTransaction transaction, OutboxMessage message, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO outbox_messages
                (id, entity_type, entity_id, operation, payload_json, created_at_utc, status, attempts, next_attempt_at_utc, last_error)
            VALUES
                ($id, $entityType, $entityId, $operation, $payloadJson, $createdAtUtc, $status, $attempts, $nextAttemptAtUtc, $lastError);
            """;

        cmd.Parameters.AddWithValue("$id", message.Id);
        cmd.Parameters.AddWithValue("$entityType", message.EntityType);
        cmd.Parameters.AddWithValue("$entityId", message.EntityId);
        cmd.Parameters.AddWithValue("$operation", message.Operation.ToString());
        cmd.Parameters.AddWithValue("$payloadJson", message.PayloadJson);
        cmd.Parameters.AddWithValue("$createdAtUtc", message.CreatedAtUtc.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$status", message.Status.ToString());
        cmd.Parameters.AddWithValue("$attempts", message.Attempts);
        cmd.Parameters.AddWithValue("$nextAttemptAtUtc", (object?)message.NextAttemptAtUtc?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lastError", (object?)message.LastError ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int maxBatchSize, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, entity_type, entity_id, operation, payload_json, created_at_utc, status, attempts, next_attempt_at_utc, last_error
            FROM outbox_messages
            WHERE status = 'Pending'
              AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= $nowUtc)
            ORDER BY created_at_utc ASC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$nowUtc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$limit", maxBatchSize);

        var result = new List<OutboxMessage>(maxBatchSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    public async Task MarkConfirmedAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var idList = ids as IReadOnlyList<string> ?? ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var transaction = connection.BeginTransaction();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE outbox_messages SET status = 'Confirmed' WHERE id = $id;";
            var idParam = cmd.Parameters.Add("$id", SqliteType.Text);
            foreach (var id in idList)
            {
                idParam.Value = id;
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkFailedAsync(string id, string error, TimeSpan nextAttemptDelay, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE outbox_messages
            SET attempts = attempts + 1,
                last_error = $error,
                next_attempt_at_utc = $nextAttemptAtUtc
            WHERE id = $id;
            """;
        cmd.Parameters.AddWithValue("$error", error);
        cmd.Parameters.AddWithValue("$nextAttemptAtUtc", DateTimeOffset.UtcNow.Add(nextAttemptDelay).ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task MoveToDeadLetterAsync(string id, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE outbox_messages SET status = 'DeadLetter' WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountPendingAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM outbox_messages WHERE status = 'Pending';";
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private static OutboxMessage Map(SqliteDataReader reader)
    {
        return new OutboxMessage(
            Id: reader.GetString(0),
            EntityType: reader.GetString(1),
            EntityId: reader.GetString(2),
            Operation: Enum.Parse<OutboxOperation>(reader.GetString(3)),
            PayloadJson: reader.GetString(4),
            CreatedAtUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
            Status: Enum.Parse<OutboxStatus>(reader.GetString(6)),
            Attempts: reader.GetInt32(7),
            NextAttemptAtUtc: reader.IsDBNull(8) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(8)),
            LastError: reader.IsDBNull(9) ? null : reader.GetString(9));
    }
}
