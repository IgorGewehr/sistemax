using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v12 do módulo "financeiro" — <c>contas_bancarias_caixa</c>: o LAR ÚNICO das
/// contas/caixas que a tela Bancário exibe e que <c>MovimentoFinanceiro.ContaBancariaCaixaId</c> já
/// referenciava como string opaca desde a F0 (docs/wiring/financeiro-telas-restantes.md §3).
/// Mutável (nome, ativa podem mudar) — upsert simples, mesmo molde de V7 (<c>recorrencias</c>).
/// </summary>
public sealed class FinanceiroSchemaMigrationV12 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 12;

    protected override string Sql =>
        """
        CREATE TABLE IF NOT EXISTS contas_bancarias_caixa (
            id                      TEXT PRIMARY KEY,
            business_id             TEXT NOT NULL,
            nome                    TEXT NOT NULL,
            tipo                    INTEGER NOT NULL,
            saldo_inicial_centavos  INTEGER NOT NULL,
            saldo_inicial_moeda     TEXT NOT NULL,
            ativa                   INTEGER NOT NULL,
            criado_em               TEXT NOT NULL,
            atualizado_em           TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_contas_bancarias_caixa_business
            ON contas_bancarias_caixa (business_id);
        """;
}
