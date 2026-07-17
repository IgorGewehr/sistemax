using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v3 do módulo "financeiro" — <c>MovimentoFinanceiro</c>, o fato de CAIXA. IMUTÁVEL por
/// invariante do agregado (nunca editado/apagado; corrigir é <c>GerarEstorno</c> — um novo
/// movimento). <c>origem_chave</c> é a coluna materializada de <c>SourceRef.Chave</c>, chave de
/// idempotência de integração.
/// </summary>
public sealed class FinanceiroSchemaMigrationV3 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 3;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS movimentos_financeiros (
            id                       TEXT PRIMARY KEY,
            business_id              TEXT NOT NULL,
            conta_bancaria_caixa_id  TEXT NOT NULL,
            forma_pagamento_id       TEXT NOT NULL,
            parcela_id               TEXT NOT NULL,
            conta_origem_id          TEXT NOT NULL,
            tipo                     INTEGER NOT NULL,
            valor_centavos           INTEGER NOT NULL,
            valor_moeda              TEXT NOT NULL,
            data_movimento           TEXT NOT NULL,
            origem_modulo            TEXT NOT NULL,
            origem_id                TEXT NOT NULL,
            origem_chave             TEXT NOT NULL,
            reversal_of_id           TEXT,
            criado_em                TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_movimentos_financeiros_business ON movimentos_financeiros (business_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_movimentos_financeiros_origem ON movimentos_financeiros (business_id, origem_chave);
        CREATE INDEX IF NOT EXISTS ix_movimentos_financeiros_reversal ON movimentos_financeiros (reversal_of_id);
        """;
}
