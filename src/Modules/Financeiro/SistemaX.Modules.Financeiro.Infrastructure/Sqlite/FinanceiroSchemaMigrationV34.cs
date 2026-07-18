using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v34 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV33"/> para
/// <c>fato_custo_diario</c>: coluna <c>projeto_id</c> NULLABLE, mesma decisão/racional (ver doc da
/// V33 — rebuild com a coluna na CHAVE fica para a fatia P5 do design, quando os folds souberem
/// escrever um <c>ProjetoId</c> real vindo dos eventos de integração).
/// </summary>
public sealed class FinanceiroSchemaMigrationV34 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 34;

    protected override string Sql =>
        """
        ALTER TABLE fato_custo_diario ADD COLUMN projeto_id TEXT NULL;
        """;
}
