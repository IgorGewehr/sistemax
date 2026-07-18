using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v29 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV28"/> para
/// <c>recorrencias</c> (docs/financeiro/design-analise-por-projeto.md §3.2): coluna
/// <c>projeto_id</c> NULLABLE, sem backfill — o lar do custo recorrente tagueado (caso Aevo).
/// </summary>
public sealed class FinanceiroSchemaMigrationV29 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 29;

    protected override string Sql =>
        """
        ALTER TABLE recorrencias ADD COLUMN projeto_id TEXT NULL;
        """;
}
