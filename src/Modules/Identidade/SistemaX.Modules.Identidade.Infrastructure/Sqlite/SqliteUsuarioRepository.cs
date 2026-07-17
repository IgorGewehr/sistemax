using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Infrastructure.Sqlite;

/// <summary>
/// Persistência real (SQLite) de <see cref="Usuario"/> — mesmo molde de
/// <c>SqliteFornecedorRepository</c> (ver docs/persistencia/persistencia-sqlite.md): schema vem
/// de <see cref="IdentidadeSchemaMigrationV1"/>, toda operação passa por
/// <see cref="ExecutarAsync"/>/<see cref="ConsultarAsync{T}"/> (participa da sessão ambiente se
/// houver uma ativa), reidratação usa <see cref="Usuario.Reconstituir"/>.
/// </summary>
public sealed class SqliteUsuarioRepository(ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao)
    : IUsuarioRepository
{
    public Task<Usuario?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                SELECT id, business_id, nome, email, papel, status, pin_hash, pin_salt, criado_em, ultimo_acesso_em
                FROM usuarios WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$id", id);

            return await LerUmAsync(cmd, ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<Usuario>> ListarAsync(string businessId, bool incluirInativos, CancellationToken ct = default)
        => ConsultarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = incluirInativos
                ? """
                  SELECT id, business_id, nome, email, papel, status, pin_hash, pin_salt, criado_em, ultimo_acesso_em
                  FROM usuarios WHERE business_id = $businessId;
                  """
                : """
                  SELECT id, business_id, nome, email, papel, status, pin_hash, pin_salt, criado_em, ultimo_acesso_em
                  FROM usuarios WHERE business_id = $businessId AND status = $status;
                  """;
            cmd.Parameters.AddWithValue("$businessId", businessId);
            if (!incluirInativos)
            {
                cmd.Parameters.AddWithValue("$status", StatusUsuario.Ativo.ToString());
            }

            var resultado = new List<Usuario>();
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                resultado.Add(LerLinha(reader));
            }

            return (IReadOnlyList<Usuario>)resultado;
        }, ct);

    public Task SalvarAsync(Usuario usuario, CancellationToken ct = default)
        => ExecutarAsync(async (connection, transaction) =>
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText =
                """
                INSERT INTO usuarios
                    (id, business_id, nome, email, papel, status, pin_hash, pin_salt, criado_em, ultimo_acesso_em)
                VALUES
                    ($id, $businessId, $nome, $email, $papel, $status, $pinHash, $pinSalt, $criadoEm, $ultimoAcessoEm)
                ON CONFLICT(id) DO UPDATE SET
                    nome             = excluded.nome,
                    email            = excluded.email,
                    papel            = excluded.papel,
                    status           = excluded.status,
                    pin_hash         = excluded.pin_hash,
                    pin_salt         = excluded.pin_salt,
                    ultimo_acesso_em = excluded.ultimo_acesso_em;
                """;
            cmd.Parameters.AddWithValue("$id", usuario.Id);
            cmd.Parameters.AddWithValue("$businessId", usuario.BusinessId);
            cmd.Parameters.AddWithValue("$nome", usuario.Nome);
            cmd.Parameters.AddWithValue("$email", usuario.Email);
            cmd.Parameters.AddWithValue("$papel", usuario.Papel.ToString());
            cmd.Parameters.AddWithValue("$status", usuario.Status.ToString());
            cmd.Parameters.AddWithValue("$pinHash", usuario.PinHash);
            cmd.Parameters.AddWithValue("$pinSalt", usuario.PinSalt);
            cmd.Parameters.AddWithValue("$criadoEm", usuario.CriadoEm.ToString("O"));
            cmd.Parameters.AddWithValue("$ultimoAcessoEm", (object?)usuario.UltimoAcessoEm?.ToString("O") ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    private static async Task<Usuario?> LerUmAsync(SqliteCommand cmd, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return LerLinha(reader);
    }

    private static Usuario LerLinha(SqliteDataReader reader)
        => Usuario.Reconstituir(
            id: reader.GetString(0),
            businessId: reader.GetString(1),
            nome: reader.GetString(2),
            email: reader.GetString(3),
            papel: Enum.Parse<Papel>(reader.GetString(4)),
            status: Enum.Parse<StatusUsuario>(reader.GetString(5)),
            pinHash: reader.GetString(6),
            pinSalt: reader.GetString(7),
            criadoEm: DateTimeOffset.Parse(reader.GetString(8)),
            ultimoAcessoEm: reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)));

    /// <summary>Escreve dentro da sessão ambiente, se houver uma ativa; senão abre conexão
    /// própria e curta — mesmo molde de <c>SqliteFornecedorRepository</c>.</summary>
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
