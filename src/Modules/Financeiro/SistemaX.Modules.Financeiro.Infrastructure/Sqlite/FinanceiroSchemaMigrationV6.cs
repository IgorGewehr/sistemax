using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v6 do módulo "financeiro" — <c>ExtratoBancarioItem</c>, linha importada de OFX/CSV.
/// IMUTÁVEL uma vez importada (insert-only). <c>identificador_externo</c> é único por
/// business_id — dedupe de reimportação do mesmo extrato.
/// </summary>
public sealed class FinanceiroSchemaMigrationV6 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 6;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS extratos_bancarios_itens (
            id                       TEXT PRIMARY KEY,
            business_id              TEXT NOT NULL,
            conta_bancaria_caixa_id  TEXT NOT NULL,
            data                     TEXT NOT NULL,
            valor_centavos           INTEGER NOT NULL,
            valor_moeda              TEXT NOT NULL,
            descricao                TEXT NOT NULL,
            identificador_externo    TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_extratos_business_conta ON extratos_bancarios_itens (business_id, conta_bancaria_caixa_id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_extratos_dedupe ON extratos_bancarios_itens (business_id, identificador_externo);
        """;
}
