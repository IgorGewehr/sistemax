using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v11 do módulo "financeiro" — <c>fato_recebiveis</c> (frente 3 da autonomia do motor
/// financeiro, docs/financeiro/inteligencia-arquitetura.md). APPEND-ONLY por construção (sem chave
/// primária de negócio — <c>id</c> autoincrementado só pra ordenação estável): DESCARTÁVEL, mesmo
/// racional de V8/V9/V10 — <c>DROP</c> (via <c>DELETE FROM</c> em <c>ZerarTudoAsync</c>) + replay do
/// <c>ProjectionRunner</c> é a correção canônica.
/// </summary>
public sealed class FinanceiroSchemaMigrationV11 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 11;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fato_recebiveis (
            id                          INTEGER PRIMARY KEY AUTOINCREMENT,
            tenant_id                   TEXT NOT NULL,
            origem_chave                TEXT NOT NULL,
            vencimento                  TEXT NOT NULL,
            data_liquidacao_prevista    TEXT NOT NULL,
            forma_pagamento             TEXT NULL,
            taxa_percentual_aplicada    TEXT NOT NULL,
            valor_bruto_centavos        INTEGER NOT NULL,
            valor_liquido_centavos      INTEGER NOT NULL,
            atualizado_em_utc           INTEGER NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_fato_recebiveis_tenant_vencimento
            ON fato_recebiveis (tenant_id, vencimento);
        """;
}
