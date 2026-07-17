using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v5 do módulo "financeiro" — <c>Conciliacao</c>, entidade raiz mutável sem filhos
/// (vínculo entre um <c>MovimentoFinanceiro</c> e um <c>ExtratoBancarioItem</c>). Upsert simples,
/// no mesmo espírito de <c>fornecedores</c> (o molde da F0).
/// </summary>
public sealed class FinanceiroSchemaMigrationV5 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 5;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS conciliacoes (
            id                       TEXT PRIMARY KEY,
            business_id              TEXT NOT NULL,
            movimento_financeiro_id  TEXT NOT NULL,
            extrato_bancario_item_id TEXT NOT NULL,
            status                   INTEGER NOT NULL,
            conciliado_em            TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_conciliacoes_business ON conciliacoes (business_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_conciliacoes_par ON conciliacoes (movimento_financeiro_id, extrato_bancario_item_id);
        """;
}
