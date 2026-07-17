using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v7 do módulo "financeiro" — <c>Recorrencia</c>, template gerador de contas futuras.
/// Entidade raiz mutável sem filhos — upsert simples.
/// </summary>
public sealed class FinanceiroSchemaMigrationV7 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 7;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS recorrencias (
            id                        TEXT PRIMARY KEY,
            business_id               TEXT NOT NULL,
            descricao                 TEXT NOT NULL,
            tipo                      INTEGER NOT NULL,
            valor_previsto_centavos   INTEGER NOT NULL,
            valor_previsto_moeda      TEXT NOT NULL,
            categoria_id              TEXT NOT NULL,
            dia_fixo                  INTEGER,
            frequencia                INTEGER NOT NULL,
            data_inicio               TEXT NOT NULL,
            data_fim                  TEXT,
            ativa                     INTEGER NOT NULL,
            ultima_geracao_em         TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_recorrencias_business_ativa ON recorrencias (business_id, ativa);
        """;
}
