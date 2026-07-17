using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v20 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV19"/> para
/// <c>fato_custo_diario</c> (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md): DROP + CREATE com
/// <c>corrente</c> na chave primária + reset do cursor de <c>fato_custo_diario</c> em
/// <c>projection_state</c> (se a tabela existir) para forçar replay completo do ledger.
/// </summary>
public sealed class FinanceiroSchemaMigrationV20 : IModuleSchemaMigration
{
    private const string SqlRebuild =
        """
        DROP TABLE IF EXISTS fato_custo_diario;

        CREATE TABLE fato_custo_diario (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            corrente          INTEGER NOT NULL,
            custo_centavos    INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia, corrente)
        );
        """;

    public string Modulo => "financeiro";

    public int Versao => 20;

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
