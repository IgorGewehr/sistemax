using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) do mapeamento corrente→anexo do Radar do Simples (P0-4,
/// docs/financeiro/revisao-domain-fit-cnpj.md) — schema em
/// <see cref="FinanceiroSchemaMigrationV22"/>. Uma linha por tenant, mapeamento serializado como
/// JSON (lista pequena — no máximo 3 correntes hoje — não justifica normalizar em várias colunas;
/// mesma escolha de simplicidade de outras configs pequenas do módulo).
/// </summary>
public sealed class SqliteConfiguracaoRadarSimplesRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IConfiguracaoRadarSimplesRepository
{
    public Task<IReadOnlyList<MapeamentoCorrenteAnexo>?> ObterAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT mapeamento_json FROM configuracao_radar_simples WHERE business_id = $biz;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            if (resultado is not string json) return (IReadOnlyList<MapeamentoCorrenteAnexo>?)null;

            return JsonSerializer.Deserialize<List<MapeamentoCorrenteAnexo>>(json);
        }, ct);

    public Task SalvarAsync(string businessId, IReadOnlyList<MapeamentoCorrenteAnexo> mapeamento, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO configuracao_radar_simples (business_id, mapeamento_json, atualizado_em_utc)
                VALUES ($biz, $json, $atualizado)
                ON CONFLICT(business_id) DO UPDATE SET
                    mapeamento_json   = excluded.mapeamento_json,
                    atualizado_em_utc = excluded.atualizado_em_utc;
                """;
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(mapeamento));
            cmd.Parameters.AddWithValue("$atualizado", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
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
