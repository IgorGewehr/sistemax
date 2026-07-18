using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v27 do módulo "financeiro" — Análise por Projeto (docs/financeiro/
/// design-analise-por-projeto.md §2.1): <c>configuracoes_financeiras</c>, o toggle opt-in por
/// tenant. Espelho de <c>fiscal_configuracoes_tenant</c> (Fiscal): uma linha por tenant, ausência
/// de linha = tudo desligado (o leitor cai em <c>ConfiguracaoFinanceiraTenant.Padrao</c>) —
/// nenhum seed necessário para tenants existentes continuarem exatamente como hoje.
/// </summary>
public sealed class FinanceiroSchemaMigrationV27 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 27;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS configuracoes_financeiras (
            tenant_id                  TEXT PRIMARY KEY,
            analise_por_projeto_ativa  INTEGER NOT NULL DEFAULT 0,
            custo_hora_padrao_centavos INTEGER NULL,
            tempo_entra_no_dre         INTEGER NOT NULL DEFAULT 0
        );
        """;
}
