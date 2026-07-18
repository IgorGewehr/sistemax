using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v26 do módulo "financeiro" — Análise por Projeto (docs/financeiro/
/// design-analise-por-projeto.md §3.1/§7), Parte A: a tabela <c>projetos</c>. Índice único
/// case-insensitive por <c>(business_id, lower(nome))</c> — nome único por tenant, mesmo racional
/// de qualquer catálogo do módulo.
/// </summary>
public sealed class FinanceiroSchemaMigrationV26 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 26;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS projetos (
            id           TEXT PRIMARY KEY,
            business_id  TEXT NOT NULL,
            nome         TEXT NOT NULL,
            descricao    TEXT NULL,
            status       INTEGER NOT NULL,
            criado_em    TEXT NOT NULL,
            arquivado_em TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_projetos_business_status ON projetos (business_id, status);

        CREATE UNIQUE INDEX IF NOT EXISTS ux_projetos_business_nome ON projetos (business_id, lower(nome));
        """;
}
