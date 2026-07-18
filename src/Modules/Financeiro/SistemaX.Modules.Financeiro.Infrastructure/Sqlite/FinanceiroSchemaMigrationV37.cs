using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v37 do módulo "financeiro" — Análise por Projeto, fatia TARDIA P5 (docs/financeiro/
/// design-analise-por-projeto.md §3.2/§7/§11): <c>fato_receita_diaria</c> ganha <c>projeto_id</c> NA
/// CHAVE — espelho BYTE-A-BYTE da estratégia de <see cref="FinanceiroSchemaMigrationV19"/> (P0-1:
/// <c>corrente</c> entrando na chave): a tabela é uma projeção DESCARTÁVEL POR CONSTRUÇÃO, então a
/// estratégia NÃO é ALTER TABLE — é DROP + CREATE com o schema novo, e resetar o cursor da projeção
/// (<c>projection_state</c>) para que o próximo catch-up refolde o ledger inteiro com a dimensão
/// nova. A chave passa de (tenant_id, dia, corrente) para (tenant_id, dia, corrente, projeto_id).
///
/// SENTINELA: <c>projeto_id TEXT NOT NULL DEFAULT ''</c> — <c>''</c> = "sem projeto" (SQLite permite
/// NULL em PK por quirk histórico; a sentinela explícita é mais segura que depender disso, mesma
/// nota do design §3.2). <c>FatoReceitaDiariaProjection</c> passa a foldar o <c>ProjetoId</c> REAL
/// de <c>CobrancaDeAssinaturaGerada</c> (a única fonte que já carrega a dimensão — P5); os demais
/// eventos (<c>VendaConcluida</c>/<c>OsFaturada</c>/<c>PedidoPago</c>) continuam gravando na
/// sentinela até a Fatia 3 da auditoria estender esses eventos (§6.2 — decisão explícita de não
/// bloquear esta fatia nisso).
///
/// A coluna já existia ADITIVA e FORA da chave desde <see cref="FinanceiroSchemaMigrationV33"/>
/// (Parte A) — este rebuild é o que a Parte A previu e adiou.
/// </summary>
public sealed class FinanceiroSchemaMigrationV37 : IModuleSchemaMigration
{
    private const string SqlRebuild =
        """
        DROP TABLE IF EXISTS fato_receita_diaria;

        CREATE TABLE fato_receita_diaria (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            corrente          INTEGER NOT NULL,
            projeto_id        TEXT NOT NULL DEFAULT '',
            receita_centavos  INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia, corrente, projeto_id)
        );
        """;

    public string Modulo => "financeiro";

    public int Versao => 37;

    public string Checksum => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SqlRebuild)));

    public async Task AplicarAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken ct)
    {
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = SqlRebuild;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }

        await FinanceiroSchemaMigrationV19.ResetarCursorSeExistirAsync(connection, transaction, "fato_receita_diaria", ct).ConfigureAwait(false);
    }
}
