using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v36 do módulo "financeiro" — Análise por Projeto, Parte B (P4, docs/financeiro/
/// design-analise-por-projeto.md §3.4/§7): a tabela <c>apontamentos_de_tempo</c>. Coluna
/// <c>custo_hora_centavos_snapshot</c> nasce PREPARADA (nullable) mas nenhum caminho de código desta
/// fatia a preenche — decisão travada do dono: só minutos por ora (ver doc da entidade).
/// </summary>
public sealed class FinanceiroSchemaMigrationV36 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 36;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS apontamentos_de_tempo (
            id                            TEXT PRIMARY KEY,
            business_id                   TEXT NOT NULL,
            projeto_id                    TEXT NULL,
            cliente_id                    TEXT NULL,
            cliente_nome                  TEXT NULL,
            assinatura_id                 TEXT NULL,
            ordem_servico_id              TEXT NULL,
            minutos                       INTEGER NOT NULL,
            data                          TEXT NOT NULL,
            operador_id                   TEXT NOT NULL,
            operador_nome                 TEXT NOT NULL,
            descricao                     TEXT NULL,
            custo_hora_centavos_snapshot  INTEGER NULL,
            criado_em                     TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_apontamentos_business_data ON apontamentos_de_tempo (business_id, data);

        CREATE INDEX IF NOT EXISTS ix_apontamentos_business_projeto ON apontamentos_de_tempo (business_id, projeto_id);

        CREATE INDEX IF NOT EXISTS ix_apontamentos_business_cliente ON apontamentos_de_tempo (business_id, cliente_id);
        """;
}
