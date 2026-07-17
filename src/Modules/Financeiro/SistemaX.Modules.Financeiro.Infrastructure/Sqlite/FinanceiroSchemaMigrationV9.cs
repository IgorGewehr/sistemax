using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v9 do módulo "financeiro" — <c>fato_custo_diario</c>, o fold que fecha o gap
/// documentado no plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
/// /ADR-0005): <c>CustoBaixadoPorVenda</c> já persistia no ledger, mas nenhuma fact table reagia.
/// Achatada por (tenant_id, dia) e DESCARTÁVEL por construção, mesmo racional de
/// <see cref="FinanceiroSchemaMigrationV8"/> — nunca edite V1-V8, já aplicadas.
/// </summary>
public sealed class FinanceiroSchemaMigrationV9 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 9;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fato_custo_diario (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            custo_centavos    INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia)
        );
        """;
}
