using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.UnitOfWork;

namespace SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

/// <summary>
/// Único helper de "escreve/consulta na sessão ambiente, se houver uma ativa; senão abre conexão
/// própria e curta" — mesmo comportamento que <c>SqliteProdutoRepository</c> (Estoque) duplica
/// método a método; aqui extraído uma vez porque os 8 repositórios SQLite deste módulo o
/// reusariam de forma idêntica (nenhuma variação de negócio entre eles nesse ponto).
/// </summary>
internal static class SqliteSessaoHelper
{
    public static async Task ExecutarAsync(
        ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao,
        Func<SqliteConnection, SqliteTransaction?, Task> acao, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            await acao(uow.Connection, uow.Transaction).ConfigureAwait(false);
            return;
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await acao(connection, null).ConfigureAwait(false);
    }

    public static async Task<T> ConsultarAsync<T>(
        ILocalSqliteConnectionFactory connectionFactory, ILocalSessao sessao,
        Func<SqliteConnection, SqliteTransaction?, Task<T>> consulta, CancellationToken ct)
    {
        if (sessao.Atual is { } uow)
        {
            return await consulta(uow.Connection, uow.Transaction).ConfigureAwait(false);
        }

        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await consulta(connection, null).ConfigureAwait(false);
    }
}
