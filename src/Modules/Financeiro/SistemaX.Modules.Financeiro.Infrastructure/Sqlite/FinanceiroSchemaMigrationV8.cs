using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v8 do módulo "financeiro" — as duas fact tables de PROVA da F0 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005):
/// <c>fato_receita_diaria</c> e <c>fato_caixa_diario</c>. Ambas são achatadas por
/// (business_id, dia) e DESCARTÁVEIS por construção — <c>DROP</c> + replay do
/// <c>ProjectionRunner</c> é a correção canônica pra qualquer bug de fold (ADR-0005 §7), nunca
/// uma migração de dado.
/// </summary>
public sealed class FinanceiroSchemaMigrationV8 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 8;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS fato_receita_diaria (
            tenant_id         TEXT NOT NULL,
            dia               TEXT NOT NULL,
            receita_centavos  INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia)
        );

        CREATE TABLE IF NOT EXISTS fato_caixa_diario (
            tenant_id          TEXT NOT NULL,
            dia                TEXT NOT NULL,
            entradas_centavos  INTEGER NOT NULL DEFAULT 0,
            saidas_centavos    INTEGER NOT NULL DEFAULT 0,
            atualizado_em_utc  INTEGER NOT NULL,
            PRIMARY KEY (tenant_id, dia)
        );
        """;
}
