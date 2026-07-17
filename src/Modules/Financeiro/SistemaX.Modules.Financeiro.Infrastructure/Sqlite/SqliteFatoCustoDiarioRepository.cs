using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <c>fato_custo_diario</c> — schema em
/// <see cref="FinanceiroSchemaMigrationV9"/>, com a coluna <c>corrente</c> entrando na chave
/// primária a partir de <see cref="FinanceiroSchemaMigrationV20"/> (P0-1,
/// docs/financeiro/revisao-domain-fit-cnpj.md). <see cref="AcumularAsync"/> é um UPSERT que SOMA
/// (não sobrescreve) sobre o valor já gravado do dia+corrente, atômico via
/// <c>ON CONFLICT DO UPDATE</c> — mesmo molde de <see cref="SqliteFatoReceitaDiariaRepository"/>.
/// </summary>
public sealed class SqliteFatoCustoDiarioRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFatoCustoDiarioRepository
{
    public Task AcumularAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, long deltaCentavos, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fato_custo_diario (tenant_id, dia, corrente, custo_centavos, atualizado_em_utc)
                VALUES ($tenantId, $dia, $corrente, $delta, $agora)
                ON CONFLICT(tenant_id, dia, corrente) DO UPDATE SET
                    custo_centavos    = custo_centavos + excluded.custo_centavos,
                    atualizado_em_utc = excluded.atualizado_em_utc;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$dia", Iso(dia));
            cmd.Parameters.AddWithValue("$corrente", (int)corrente);
            cmd.Parameters.AddWithValue("$delta", deltaCentavos);
            cmd.Parameters.AddWithValue("$agora", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<FatoCustoDiario?> ObterAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT tenant_id, dia, corrente, custo_centavos, atualizado_em_utc FROM fato_custo_diario WHERE tenant_id = $tenantId AND dia = $dia AND corrente = $corrente;";
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$dia", Iso(dia));
            cmd.Parameters.AddWithValue("$corrente", (int)corrente);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }
            return Ler(reader);
        }, ct);

    public Task<IReadOnlyList<FatoCustoDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT tenant_id, dia, corrente, custo_centavos, atualizado_em_utc
                FROM fato_custo_diario
                WHERE tenant_id = $tenantId AND dia >= $de AND dia <= $ate
                ORDER BY dia ASC, corrente ASC;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$de", Iso(de));
            cmd.Parameters.AddWithValue("$ate", Iso(ate));

            var resultado = new List<FatoCustoDiario>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Ler(reader));
            }
            return (IReadOnlyList<FatoCustoDiario>)resultado;
        }, ct);

    public Task ZerarTudoAsync(CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM fato_custo_diario;";
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static FatoCustoDiario Ler(SqliteDataReader reader)
        => new(
            TenantId: reader.GetString(0),
            Dia: DateOnly.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
            Corrente: (CorrenteDeReceita)reader.GetInt32(2),
            CustoCentavos: reader.GetInt64(3),
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
