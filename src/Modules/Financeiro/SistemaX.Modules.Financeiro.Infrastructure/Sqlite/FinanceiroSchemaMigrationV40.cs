using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v40 do módulo "financeiro" — Imobilizado/Painel de ROI (docs/financeiro/
/// design-imobilizado-roi.md §3.3/§9): a tabela <c>aportes_de_capital</c> — registro LEVE,
/// gerencial, de capital de giro/investimento inicial. Sem FK contábil nenhuma (o aporte é
/// deliberadamente fora da partida dobrada — ver a doc do agregado <c>AporteDeCapital</c>).
/// </summary>
public sealed class FinanceiroSchemaMigrationV40 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 40;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS aportes_de_capital (
            id              TEXT PRIMARY KEY,
            business_id     TEXT NOT NULL,
            valor_centavos  INTEGER NOT NULL,
            data            TEXT NOT NULL,
            descricao       TEXT NOT NULL,
            criado_em       TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_aportes_de_capital_business_data ON aportes_de_capital (business_id, data);
        """;
}
