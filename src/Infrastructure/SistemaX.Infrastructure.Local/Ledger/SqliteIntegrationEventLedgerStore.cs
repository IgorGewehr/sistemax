using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Ids;
using SistemaX.Modules.Abstractions.Runtime;

namespace SistemaX.Infrastructure.Local.Ledger;

/// <inheritdoc cref="IIntegrationEventLedgerStore"/>
/// <remarks>
/// Persistência REAL (SQLite) do ledger append-only <c>integration_events</c> — schema em
/// <see cref="Migrations.IntegrationEventsSchemaMigration"/>. Sempre SQLite (não há adapter em
/// memória: o ledger é a fonte de verdade histórica, mesmo racional de <c>SqliteOutboxStore</c>
/// não ter par em memória).
///
/// <see cref="AppendAsync"/> abre sua PRÓPRIA conexão curta (não participa da sessão ambiente
/// <c>ILocalSessao</c> de propósito): o bus (<see cref="Runtime.InProcessIntegrationEventBus"/>)
/// roda num escopo de DI NOVO por publicação (<c>scopeFactory.CreateAsyncScope()</c>), então não
/// haveria uma <c>ILocalSessao</c> ambiente pra reaproveitar de qualquer forma. O INSERT é uma
/// única instrução — atômico por natureza no SQLite, sem precisar de transação explícita.
/// </remarks>
public sealed class SqliteIntegrationEventLedgerStore(ILocalSqliteConnectionFactory connectionFactory) : IIntegrationEventLedgerStore
{
    public async Task<bool> AppendAsync(
        string tipo, string tenantId, string payloadJson, DateTimeOffset ocorridoEm, string chaveIdempotencia, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO integration_events
                (id, tipo, tenant_id, payload_json, ocorrido_em, chave_idempotencia, persistido_em_utc)
            VALUES
                ($id, $tipo, $tenantId, $payloadJson, $ocorridoEm, $chaveIdempotencia, $persistidoEmUtc)
            ON CONFLICT(chave_idempotencia) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$id", UlidGenerator.NewUlid());
        cmd.Parameters.AddWithValue("$tipo", tipo);
        cmd.Parameters.AddWithValue("$tenantId", tenantId);
        cmd.Parameters.AddWithValue("$payloadJson", payloadJson);
        cmd.Parameters.AddWithValue("$ocorridoEm", ocorridoEm.ToString("O"));
        cmd.Parameters.AddWithValue("$chaveIdempotencia", chaveIdempotencia);
        cmd.Parameters.AddWithValue("$persistidoEmUtc", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var linhasAfetadas = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return linhasAfetadas > 0;
    }

    public async Task<IReadOnlyList<IntegrationEventLedgerEntry>> LerAPartirDoCursorAsync(
        long afterCursor, int maxBatchSize, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT seq, id, tipo, tenant_id, payload_json, ocorrido_em, chave_idempotencia, persistido_em_utc
            FROM integration_events
            WHERE seq > $afterCursor
            ORDER BY seq ASC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$afterCursor", afterCursor);
        cmd.Parameters.AddWithValue("$limit", maxBatchSize);

        var resultado = new List<IntegrationEventLedgerEntry>(maxBatchSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            resultado.Add(new IntegrationEventLedgerEntry(
                Cursor: reader.GetInt64(0),
                Id: reader.GetString(1),
                Tipo: reader.GetString(2),
                TenantId: reader.GetString(3),
                PayloadJson: reader.GetString(4),
                OcorridoEm: DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                ChaveIdempotencia: reader.GetString(6),
                PersistidoEmUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7))));
        }

        return resultado;
    }

    public async Task<long> ObterUltimoCursorAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(seq), 0) FROM integration_events;";
        var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt64(resultado);
    }
}
