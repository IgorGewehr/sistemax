using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v39 do módulo "financeiro" — Imobilizado/Painel de ROI (docs/financeiro/
/// design-imobilizado-roi.md §2.1/§9): o SEGUNDO toggle opt-in de <c>configuracoes_financeiras</c>,
/// independente de <c>analise_por_projeto_ativa</c> (o dono pode ligar Imobilizado+ROI no dia zero
/// e a Análise por Projeto só meses depois, ou vice-versa). ALTERs aditivos, todos com DEFAULT que
/// reproduz o comportamento de hoje (desligado/omitido) — nenhuma linha existente muda de
/// significado.
/// </summary>
public sealed class FinanceiroSchemaMigrationV39 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 39;

    protected override string Sql =>
        """
        ALTER TABLE configuracoes_financeiras ADD COLUMN imobilizado_roi_ativo INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE configuracoes_financeiras ADD COLUMN taxa_desconto_anual_bps INTEGER NULL;
        ALTER TABLE configuracoes_financeiras ADD COLUMN inicio_operacao TEXT NULL;
        """;
}
