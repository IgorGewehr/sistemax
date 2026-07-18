using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v38 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV37"/> para
/// <c>fato_custo_diario</c> (P5, docs/financeiro/design-analise-por-projeto.md §11): DROP + CREATE
/// com <c>projeto_id</c> na chave (sentinela <c>''</c> = sem projeto) + reset do cursor. Nenhum
/// fold escreve <c>ProjetoId</c> real aqui ainda (decisão explícita: <c>fato_custo_diario</c>
/// continua CMV puro nesta fatia, sem amortização — ver a doc do evento <c>CustoAmortizadoReconhecido</c>
/// no catálogo) — a coluna nasce na chave só para não exigir OUTRO rebuild quando isso mudar.
/// </summary>
public sealed class FinanceiroSchemaMigrationV38 : IModuleSchemaMigration
{
    private const string SqlRebuild =
        """
        DROP TABLE IF EXISTS fato_custo_diario;

        CREATE TABLE fato_custo_diario (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            corrente          INTEGER NOT NULL,
            projeto_id        TEXT NOT NULL DEFAULT '',
            custo_centavos    INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia, corrente, projeto_id)
        );
        """;

    public string Modulo => "financeiro";

    public int Versao => 38;

    public string Checksum => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SqlRebuild)));

    public async Task AplicarAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = SqlRebuild;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await FinanceiroSchemaMigrationV19.ResetarCursorSeExistirAsync(connection, transaction, "fato_custo_diario", ct).ConfigureAwait(false);
    }
}
