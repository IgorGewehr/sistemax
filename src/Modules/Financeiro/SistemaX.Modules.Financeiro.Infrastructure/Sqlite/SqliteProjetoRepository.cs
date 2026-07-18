using System.Globalization;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Projetos;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Projeto"/> — mesmo molde de
/// <see cref="SqliteAssinaturaRepository"/>. Schema nasce de <see cref="FinanceiroSchemaMigrationV26"/>.
/// </summary>
public sealed class SqliteProjetoRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IProjetoRepository
{
    private const string Colunas = "id, business_id, nome, descricao, status, criado_em, arquivado_em";

    public Task<IReadOnlyList<Projeto>> ListarAsync(string businessId, bool incluirArquivados, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = incluirArquivados
                ? $"SELECT {Colunas} FROM projetos WHERE business_id = $biz ORDER BY criado_em;"
                : $"SELECT {Colunas} FROM projetos WHERE business_id = $biz AND status = $status ORDER BY criado_em;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            if (!incluirArquivados) cmd.Parameters.AddWithValue("$status", (int)StatusProjeto.Ativo);

            var resultado = new List<Projeto>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false)) resultado.Add(Ler(reader));
            return (IReadOnlyList<Projeto>)resultado;
        }, ct);

    public Task<Projeto?> ObterPorIdAsync(string businessId, string projetoId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM projetos WHERE business_id = $biz AND id = $id;";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$id", projetoId);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task<Projeto?> BuscarPorNomeAsync(string businessId, string nome, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {Colunas} FROM projetos WHERE business_id = $biz AND lower(nome) = lower($nome);";
            cmd.Parameters.AddWithValue("$biz", businessId);
            cmd.Parameters.AddWithValue("$nome", nome);

            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            return await reader.ReadAsync(ct).ConfigureAwait(false) ? Ler(reader) : null;
        }, ct);

    public Task SalvarAsync(Projeto projeto, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO projetos (id, business_id, nome, descricao, status, criado_em, arquivado_em)
                VALUES ($id, $biz, $nome, $descricao, $status, $criadoEm, $arquivadoEm)
                ON CONFLICT(id) DO UPDATE SET
                    nome = excluded.nome, descricao = excluded.descricao, status = excluded.status, arquivado_em = excluded.arquivado_em;
                """;
            cmd.Parameters.AddWithValue("$id", projeto.Id);
            cmd.Parameters.AddWithValue("$biz", projeto.BusinessId);
            cmd.Parameters.AddWithValue("$nome", projeto.Nome);
            cmd.Parameters.AddWithValue("$descricao", (object?)projeto.Descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)projeto.Status);
            cmd.Parameters.AddWithValue("$criadoEm", Iso(projeto.CriadoEm));
            cmd.Parameters.AddWithValue("$arquivadoEm", (object?)Iso(projeto.ArquivadoEm) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static Projeto Ler(SqliteDataReader reader)
        => Projeto.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            nome: reader.GetString(2),
            descricao: reader.IsDBNull(3) ? null : reader.GetString(3),
            status: (StatusProjeto)reader.GetInt32(4),
            criadoEm: ParseData(reader.GetString(5))!.Value,
            arquivadoEm: reader.IsDBNull(6) ? null : ParseData(reader.GetString(6)));

    private static string Iso(DateTimeOffset d) => d.ToString("O", CultureInfo.InvariantCulture);
    private static string? Iso(DateTimeOffset? d) => d?.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseData(string? s) => s is null ? null : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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
