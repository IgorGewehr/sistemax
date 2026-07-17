using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v1 do módulo "financeiro" — primeira persistência real do módulo além do antigo
/// <c>SqliteAssinaturaRepository</c> (que nunca usou este mecanismo de <c>IModuleSchemaMigration</c>).
/// Tabela <c>contas_a_receber</c> (header) + <c>parcelas_a_receber</c> (filha mutável, FK
/// <c>ON DELETE CASCADE</c>). <c>source_ref_chave</c> é a coluna MATERIALIZADA de
/// <c>SourceRef.Chave</c> (<c>$"{Modulo}:{Id}"</c>) — evita recompor a chave em SQL, permitindo
/// índice único simples por (business_id, source_ref_chave) para a idempotência de integração
/// (docs/financeiro-datamodel.md §4.3).
/// </summary>
public sealed class FinanceiroSchemaMigrationV1 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 1;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS contas_a_receber (
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
            cliente_id            TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_contas_a_receber_business ON contas_a_receber (business_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_contas_a_receber_origem ON contas_a_receber (business_id, source_ref_chave);

        CREATE TABLE IF NOT EXISTS parcelas_a_receber (
            id                  TEXT PRIMARY KEY,
            conta_id            TEXT NOT NULL REFERENCES contas_a_receber(id) ON DELETE CASCADE,
            numero              INTEGER NOT NULL,
            vencimento          TEXT NOT NULL,
            valor_centavos      INTEGER NOT NULL,
            valor_moeda         TEXT NOT NULL,
            valor_pago_centavos INTEGER NOT NULL,
            status              INTEGER NOT NULL,
            data_liquidacao     TEXT,
            forma_pagamento_id  TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_parcelas_a_receber_conta ON parcelas_a_receber (conta_id);
        """;
}
