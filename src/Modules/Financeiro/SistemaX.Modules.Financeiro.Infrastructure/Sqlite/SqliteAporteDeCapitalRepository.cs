using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="AporteDeCapital"/> — mesmo molde de
/// <see cref="SqliteAtivoDeCapitalRepository"/>. Schema nasce de <see cref="FinanceiroSchemaMigrationV40"/>.
/// </summary>
public sealed class SqliteAporteDeCapitalRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IAporteDeCapitalRepository
{
    private const string Colunas = "id, business_id, valor_centavos, data, descricao, criado_em";

    public Task<AporteDeCapital?> ObterPorIdAsync(string businessId, string aporteId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM aportes_de_capital WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", aporteId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<IReadOnlyList<AporteDeCapital>> ListarAsync(string businessId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM aportes_de_capital WHERE business_id = $biz ORDER BY data;";
            cmd.Parameters.AddWithValue("$biz", businessId);

            var resultado = new List<AporteDeCapital>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<AporteDeCapital>)resultado;
        }, ct);

    public Task SalvarAsync(AporteDeCapital aporte, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO aportes_de_capital (id, business_id, valor_centavos, data, descricao, criado_em)
                VALUES ($id, $biz, $valor, $data, $descricao, $criadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    valor_centavos = excluded.valor_centavos,
                    data           = excluded.data,
                    descricao      = excluded.descricao;
                """;
            cmd.Parameters.AddWithValue("$id", aporte.Id);
            cmd.Parameters.AddWithValue("$biz", aporte.BusinessId);
            cmd.Parameters.AddWithValue("$valor", aporte.Valor.Centavos);
            cmd.Parameters.AddWithValue("$data", IsoData(aporte.Data));
            cmd.Parameters.AddWithValue("$descricao", aporte.Descricao);
            cmd.Parameters.AddWithValue("$criadoEm", IsoInstante(aporte.CriadoEm));
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<bool> ExcluirAsync(string businessId, string aporteId, CancellationToken ct = default)
        => ExecutarComResultadoAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM aportes_de_capital WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", aporteId);
            var linhas = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return linhas > 0;
        }, ct);

    private static AporteDeCapital Ler(SqliteDataReader reader)
        => AporteDeCapital.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            valor: new Money(reader.GetInt64(2)),
            data: ParseData(reader.GetString(3)),
            descricao: reader.GetString(4),
            criadoEm: ParseInstante(reader.GetString(5))!.Value);

    private static string IsoData(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static DateOnly ParseData(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string IsoInstante(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseInstante(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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

    private async Task<T> ExecutarComResultadoAsync<T>(Func<SqliteConnection, SqliteTransaction?, Task<T>> acao, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            return await acao(uow.Connection, uow.Transaction).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await acao(connection, null).ConfigureAwait(false);
    }
}
