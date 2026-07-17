namespace SistemaX.Infrastructure.Local.Projections;

/// <inheritdoc cref="IProjectionStateStore"/>
public sealed class SqliteProjectionStateStore(ILocalSqliteConnectionFactory connectionFactory) : IProjectionStateStore
{
    public async Task<long> ObterCursorAsync(string nomeProjecao, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT ultimo_cursor_processado FROM projection_state WHERE nome = $nome;";
        cmd.Parameters.AddWithValue("$nome", nomeProjecao);

        var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return resultado is null or DBNull ? 0L : Convert.ToInt64(resultado);
    }

    public async Task SalvarCursorAsync(string nomeProjecao, long cursor, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO projection_state (nome, ultimo_cursor_processado, atualizado_em_utc)
            VALUES ($nome, $cursor, $atualizadoEm)
            ON CONFLICT(nome) DO UPDATE SET
                ultimo_cursor_processado = excluded.ultimo_cursor_processado,
                atualizado_em_utc        = excluded.atualizado_em_utc;
            """;
        cmd.Parameters.AddWithValue("$nome", nomeProjecao);
        cmd.Parameters.AddWithValue("$cursor", cursor);
        cmd.Parameters.AddWithValue("$atualizadoEm", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
