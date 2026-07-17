using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Sequences;

/// <inheritdoc cref="ILocalSequenceAllocator"/>
public sealed class LocalSequenceAllocator : ILocalSequenceAllocator
{
    public async Task<long> NextAsync(SqliteConnection connection, SqliteTransaction transaction, string sequenceName, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        // UPSERT atômico: cria a sequência em 1 se não existir, senão incrementa — uma única
        // ida ao banco, sem race entre "SELECT valor" e "UPDATE valor+1" porque roda dentro da
        // MESMA transação do Unit-of-Work do chamador (SQLite serializa escritores).
        cmd.CommandText =
            """
            INSERT INTO local_sequences (name, value) VALUES ($name, 1)
            ON CONFLICT(name) DO UPDATE SET value = value + 1
            RETURNING value;
            """;
        cmd.Parameters.AddWithValue("$name", sequenceName);

        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }
}
