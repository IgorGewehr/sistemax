using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v2 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV1"/> para
/// <c>ContaAPagar</c>: tabela <c>contas_a_pagar</c> (header) + <c>parcelas_a_pagar</c> (filha
/// mutável), com <c>fornecedor_id</c> no lugar de <c>cliente_id</c>.
/// </summary>
public sealed class FinanceiroSchemaMigrationV2 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 2;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS contas_a_pagar (
            id                    TEXT PRIMARY KEY,
            business_id           TEXT NOT NULL,
            source_ref_modulo     TEXT NOT NULL,
            source_ref_id         TEXT NOT NULL,
            source_ref_chave      TEXT NOT NULL,
            descricao             TEXT NOT NULL,
            categoria_id          TEXT NOT NULL,
            centro_de_custo_id    TEXT,
            data_competencia      TEXT NOT NULL,
            valor_total_centavos  INTEGER NOT NULL,
            valor_total_moeda     TEXT NOT NULL,
            status                INTEGER NOT NULL,
            criado_em             TEXT NOT NULL,
            fornecedor_id         TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_contas_a_pagar_business ON contas_a_pagar (business_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_contas_a_pagar_origem ON contas_a_pagar (business_id, source_ref_chave);

        CREATE TABLE IF NOT EXISTS parcelas_a_pagar (
            id                  TEXT PRIMARY KEY,
            conta_id            TEXT NOT NULL REFERENCES contas_a_pagar(id) ON DELETE CASCADE,
            numero              INTEGER NOT NULL,
            vencimento          TEXT NOT NULL,
            valor_centavos      INTEGER NOT NULL,
            valor_moeda         TEXT NOT NULL,
            valor_pago_centavos INTEGER NOT NULL,
            status              INTEGER NOT NULL,
            data_liquidacao     TEXT,
            forma_pagamento_id  TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_parcelas_a_pagar_conta ON parcelas_a_pagar (conta_id);
        """;
}
