using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v28 do módulo "financeiro" — Análise por Projeto (docs/financeiro/
/// design-analise-por-projeto.md §3.2/§7): <c>assinaturas</c> ganha <c>projeto_id</c> NULLABLE.
/// SEM BACKFILL (§3.2 do design: projeto é conceito novo, não existe pista no dado histórico,
/// diferente de <c>corrente</c> que era inferível por <c>SourceRef.Modulo</c>) — linhas antigas
/// ficam <c>NULL</c> ("sem projeto"), comportamento de hoje intacto.
/// </summary>
public sealed class FinanceiroSchemaMigrationV28 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 28;

    protected override string Sql =>
        """
        ALTER TABLE assinaturas ADD COLUMN projeto_id TEXT NULL;
        """;
}
