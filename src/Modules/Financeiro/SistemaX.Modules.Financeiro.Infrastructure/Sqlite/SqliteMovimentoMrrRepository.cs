using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) do ledger de <see cref="MovimentoMrr"/> (P1-4) — append-only, mesmo
/// molde de <c>SqliteLancamentoContabilRepository</c>: <c>INSERT</c> puro, nunca <c>UPDATE</c>
/// (histórico imutável de fatos).
/// </summary>
public sealed class SqliteMovimentoMrrRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IMovimentoMrrRepository
{
    public Task RegistrarAsync(MovimentoMrr movimento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO mrr_movimentos
                    (id, business_id, assinatura_id, servico_id, tipo, valor_centavos, competencia, ocorrido_em)
                VALUES
                    ($id, $biz, $assId, $srvId, $tipo, $valor, $competencia, $ocorridoEm);
                """;
            cmd.Parameters.AddWithValue("$id", movimento.Id);
            cmd.Parameters.AddWithValue("$biz", movimento.BusinessId);
            cmd.Parameters.AddWithValue("$assId", movimento.AssinaturaId);
            cmd.Parameters.AddWithValue("$srvId", movimento.ServicoId);
            cmd.Parameters.AddWithValue("$tipo", (int)movimento.Tipo);
            cmd.Parameters.AddWithValue("$valor", movimento.ValorCentavos);
            cmd.Parameters.AddWithValue("$competencia", movimento.Competencia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$ocorridoEm", movimento.OcorridoEm.ToString("O", CultureInfo.InvariantCulture));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<MovimentoMrr>> ListarAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT id, business_id, assinatura_id, servico_id, tipo, valor_centavos, competencia, ocorrido_em
                FROM mrr_movimentos
                WHERE business_id = $biz
                ORDER BY competencia, ocorrido_em;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<MovimentoMrr>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(new MovimentoMrr(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    (TipoMovimentoMrr)reader.GetInt32(4), reader.GetInt64(5),
                    DateOnly.Parse(reader.GetString(6), CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
            }
            return (IReadOnlyList<MovimentoMrr>)resultado;
        }, ct);

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
