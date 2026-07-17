using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v19 do módulo "financeiro" — <c>fato_receita_diaria</c> ganha a dimensão "corrente de
/// receita" na CHAVE (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md): a chave passa de
/// (tenant_id, dia) para (tenant_id, dia, corrente). Como a tabela é uma projeção DESCARTÁVEL POR
/// CONSTRUÇÃO (ver <see cref="FinanceiroSchemaMigrationV8"/> — "DROP + replay do ProjectionRunner é
/// a correção canônica pra qualquer bug de fold, nunca uma migração de dado"), a estratégia aqui NÃO
/// é ALTER TABLE: é DROP + CREATE com o schema novo, e resetar o cursor da projeção
/// (<c>projection_state</c>) para que o próximo catch-up refolde o ledger inteiro com a dimensão
/// nova — o MESMO mecanismo usado para qualquer replay manual (<c>ProjectionRunner.ReconstruirAsync</c>),
/// só que disparado automaticamente pela migração em vez de uma chamada explícita.
///
/// GUARDA: <c>projection_state</c> pertence ao módulo "local" (infraestrutura de ledger/projeções),
/// que migra ANTES de "financeiro" em produção (ordem topológica de módulos — dependido antes de
/// dependente). Mas testes de contrato isolados deste repositório às vezes aplicam só as migrações
/// do próprio módulo financeiro — por isso o reset do cursor é condicional à tabela já existir,
/// nunca assumido.
/// </summary>
public sealed class FinanceiroSchemaMigrationV19 : IModuleSchemaMigration
{
    private const string SqlRebuild =
        """
        DROP TABLE IF EXISTS fato_receita_diaria;

        CREATE TABLE fato_receita_diaria (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            corrente          INTEGER NOT NULL,
            receita_centavos  INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia, corrente)
        );
        """;

    public string Modulo => "financeiro";

    public int Versao => 19;

    public string Checksum => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SqlRebuild)));

    public async Task AplicarAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = SqlRebuild;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await ResetarCursorSeExistirAsync(connection, transaction, "fato_receita_diaria", ct).ConfigureAwait(false);
    }

    /// <summary>Compartilhado com <see cref="FinanceiroSchemaMigrationV20"/> — deixa explícito que
    /// as duas migrações têm o MESMO racional de reset, não uma coincidência.</summary>
    internal static async Task ResetarCursorSeExistirAsync(SqliteConnection connection, SqliteTransaction transaction, string nomeProjecao, CancellationToken ct)
    {
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.Transaction = transaction;
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'projection_state';";
            var existe = Convert.ToInt64(await checkCmd.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
            if (!existe) return;
        }

        await using var delCmd = connection.CreateCommand();
        delCmd.Transaction = transaction;
        delCmd.CommandText = "DELETE FROM projection_state WHERE nome = $nome;";
        delCmd.Parameters.AddWithValue("$nome", nomeProjecao);
        await delCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
