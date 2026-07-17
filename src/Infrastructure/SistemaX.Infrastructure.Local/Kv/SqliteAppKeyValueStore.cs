namespace SistemaX.Infrastructure.Local.Kv;

/// <inheritdoc cref="IAppKeyValueStore"/>
public sealed class SqliteAppKeyValueStore(ILocalSqliteConnectionFactory connectionFactory) : IAppKeyValueStore
{
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_kv WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result as string;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO app_kv (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
