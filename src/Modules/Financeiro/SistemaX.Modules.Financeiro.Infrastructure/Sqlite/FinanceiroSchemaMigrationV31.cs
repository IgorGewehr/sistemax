using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v31 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV17"/> para
/// <c>contas_a_pagar</c> (docs/financeiro/design-analise-por-projeto.md §3.2): coluna
/// <c>projeto_id</c> NULLABLE, sem backfill.
/// </summary>
public sealed class FinanceiroSchemaMigrationV31 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 31;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_pagar ADD COLUMN projeto_id TEXT NULL;
        """;
}
