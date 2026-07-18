using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v30 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV28"/> para
/// <c>contas_a_receber</c> (docs/financeiro/design-analise-por-projeto.md §3.2, tabela de
/// tagging): coluna <c>projeto_id</c> NULLABLE, sem backfill — irmã de <c>corrente</c>
/// (<see cref="FinanceiroSchemaMigrationV16"/>), mesmo ponto do construtor/<c>Reconstituir</c>.
/// </summary>
public sealed class FinanceiroSchemaMigrationV30 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 30;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_receber ADD COLUMN projeto_id TEXT NULL;
        """;
}
