using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <c>fato_caixa_diario</c> — schema em
/// <see cref="FinanceiroSchemaMigrationV8"/>. Entradas e saídas acumulam de forma independente
/// (dois UPSERTs que somam, nunca sobrescrevem); o saldo do dia é sempre derivado na leitura.
/// </summary>
public sealed class SqliteFatoCaixaDiarioRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFatoCaixaDiarioRepository
{
    public Task AcumularEntradaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default)
        => AcumularAsync(tenantId, dia, entradaDelta: deltaCentavos, saidaDelta: 0, ct);

    public Task AcumularSaidaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default)
        => AcumularAsync(tenantId, dia, entradaDelta: 0, saidaDelta: deltaCentavos, ct);

    private Task AcumularAsync(string tenantId, DateOnly dia, long entradaDelta, long saidaDelta, CancellationToken ct)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fato_caixa_diario (tenant_id, dia, entradas_centavos, saidas_centavos, atualizado_em_utc)
                VALUES ($tenantId, $dia, $entradaDelta, $saidaDelta, $agora)
                ON CONFLICT(tenant_id, dia) DO UPDATE SET
                    entradas_centavos = entradas_centavos + excluded.entradas_centavos,
                    saidas_centavos   = saidas_centavos + excluded.saidas_centavos,
                    atualizado_em_utc = excluded.atualizado_em_utc;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$dia", Iso(dia));
            cmd.Parameters.AddWithValue("$entradaDelta", entradaDelta);
            cmd.Parameters.AddWithValue("$saidaDelta", saidaDelta);
            cmd.Parameters.AddWithValue("$agora", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<FatoCaixaDiario?> ObterAsync(string tenantId, DateOnly dia, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT tenant_id, dia, entradas_centavos, saidas_centavos, atualizado_em_utc FROM fato_caixa_diario WHERE tenant_id = $tenantId AND dia = $dia;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$dia", Iso(dia));

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }
            return Ler(reader);
        }, ct);

    public Task<IReadOnlyList<FatoCaixaDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT tenant_id, dia, entradas_centavos, saidas_centavos, atualizado_em_utc
                FROM fato_caixa_diario
                WHERE tenant_id = $tenantId AND dia >= $de AND dia <= $ate
                ORDER BY dia ASC;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));

            var resultado = new List<FatoCaixaDiario>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<FatoCaixaDiario>)resultado;
        }, ct);

    public Task ZerarTudoAsync(CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM fato_caixa_diario;";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static FatoCaixaDiario Ler(SqliteDataReader reader)
        => new(
            TenantId: reader.GetString(0),
            Dia: DateOnly.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
            EntradasCentavos: reader.GetInt64(2),
            SaidasCentavos: reader.GetInt64(3),
            AtualizadoEmUtc: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)));

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task ExecutarAsync(Func<SqliteConnection, SqliteTransaction?, Task> acao, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            await acao(uow.Connection, uow.Transaction).ConfigureAwait(false);
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await acao(connection, null).ConfigureAwait(false);
    }

    private async Task<T> ConsultarAsync<T>(Func<SqliteConnection, SqliteTransaction?, Task<T>> consulta, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            return await consulta(uow.Connection, uow.Transaction).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await consulta(connection, null).ConfigureAwait(false);
    }
}
