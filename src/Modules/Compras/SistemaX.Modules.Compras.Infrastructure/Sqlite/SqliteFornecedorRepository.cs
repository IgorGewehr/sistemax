using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Fornecedores;

namespace SistemaX.Modules.Compras.Infrastructure.Sqlite;

/// <summary>
/// Persistência REAL (SQLite) de <see cref="Fornecedor"/> — o REPOSITÓRIO-MOLDE da F0
/// (ver docs/persistencia/persistencia-sqlite.md): copie esta classe para portar qualquer um dos
/// outros 12 ports ainda in-memory na F1. Pontos a repetir em cada porte novo:
///
///  1. Schema vem de uma <see cref="ComprasSchemaMigrationV1"/> (<c>IModuleSchemaMigration</c>),
///     NUNCA de DDL no construtor do repositório — diferente do antigo <c>SqliteAssinaturaRepository</c>,
///     que criava a tabela em <c>Inicializar()</c> a cada instanciação.
///  2. Toda operação passa por <see cref="ExecutarAsync"/>/<see cref="ConsultarAsync{T}"/>: se há
///     uma <see cref="ILocalSessao"/> ATIVA (um caso de uso chamou <c>IniciarAsync</c> antes de
///     chegar aqui), a operação participa da MESMA conexão/transação; senão abre uma conexão
///     própria e curta (leituras soltas fora de um caso de uso orquestrado — barato com WAL).
///  3. Reidratação usa <see cref="Fornecedor.Reconstituir"/> — nunca o construtor de negócio
///     (<see cref="Fornecedor.Cadastrar"/>), que validaria de novo e dispararia evento de criação.
///  4. Mesmo port (<see cref="IFornecedorRepository"/>) que <c>InMemoryFornecedorRepository</c> —
///     trocar um pelo outro (ver <c>ComprasInfrastructureModule</c>) não toca Domain/Application.
/// </summary>
public sealed class SqliteFornecedorRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IFornecedorRepository
{
    public Task<Fornecedor?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                "SELECT id, tenant_id, documento, razao_social, nome_fantasia, status FROM fornecedores WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<Fornecedor?> ObterPorDocumentoAsync(string tenantId, string documento, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT id, tenant_id, documento, razao_social, nome_fantasia, status
                FROM fornecedores
                WHERE tenant_id = $tenantId AND documento = $documento;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);
            cmd.Parameters.AddWithValue("$documento", documento);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task SalvarAsync(Fornecedor fornecedor, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO fornecedores (id, tenant_id, documento, razao_social, nome_fantasia, status)
                VALUES ($id, $tenantId, $documento, $razaoSocial, $nomeFantasia, $status)
                ON CONFLICT(id) DO UPDATE SET
                    razao_social  = excluded.razao_social,
                    nome_fantasia = excluded.nome_fantasia,
                    documento     = excluded.documento,
                    status        = excluded.status;
                """;
            cmd.Parameters.AddWithValue("$id", fornecedor.Id);
            cmd.Parameters.AddWithValue("$tenantId", fornecedor.TenantId);
            cmd.Parameters.AddWithValue("$documento", (object?)fornecedor.Documento ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$razaoSocial", fornecedor.RazaoSocial);
            cmd.Parameters.AddWithValue("$nomeFantasia", (object?)fornecedor.NomeFantasia ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)fornecedor.Status);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<Fornecedor>> ListarAsync(string tenantId, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT id, tenant_id, documento, razao_social, nome_fantasia, status
                FROM fornecedores
                WHERE tenant_id = $tenantId
                ORDER BY razao_social;
                """;
            cmd.Parameters.AddWithValue("$tenantId", tenantId);

            var resultado = new List<Fornecedor>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(Fornecedor.Reconstituir(
                    id: reader.GetString(0),
                    tenantId: reader.GetString(1),
                    razaoSocial: reader.GetString(3),
                    documento: reader.IsDBNull(2) ? null : reader.GetString(2),
                    nomeFantasia: reader.IsDBNull(4) ? null : reader.GetString(4),
                    status: (StatusFornecedor)reader.GetInt32(5)));
            }

            return (IReadOnlyList<Fornecedor>)resultado;
        }, ct);

    private static async Task<Fornecedor?> LerUmAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return Fornecedor.Reconstituir(
            id: reader.GetString(0),
            tenantId: reader.GetString(1),
            razaoSocial: reader.GetString(3),
            documento: reader.IsDBNull(2) ? null : reader.GetString(2),
            nomeFantasia: reader.IsDBNull(4) ? null : reader.GetString(4),
            status: (StatusFornecedor)reader.GetInt32(5));
    }

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão própria
    /// e curta. Este método (e <see cref="ConsultarAsync{T}"/>) é o que TODO repositório SQLite
    /// novo deve reusar — nunca abrir conexão "na mão" espalhado pelos métodos do port.</summary>
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
