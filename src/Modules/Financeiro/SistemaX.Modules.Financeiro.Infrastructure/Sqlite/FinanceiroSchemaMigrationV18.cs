using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v18 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV16"/> para
/// <c>MovimentoFinanceiro</c> (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md): coluna
/// <c>corrente</c> NULLABLE + backfill pelo <c>origem_modulo</c> (o mesmo sinal materializado que
/// <see cref="FinanceiroSchemaMigrationV16"/> usa via <c>SourceRef.Modulo</c> de
/// <c>ContaAReceber</c>) — <c>sale-payment</c>/<c>order-payment-caixa</c> são sempre a liquidação à
/// vista de uma venda/pedido de balcão, portanto Comercio.
/// </summary>
public sealed class FinanceiroSchemaMigrationV18 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 18;

    protected override string Sql =>
        """
        ALTER TABLE movimentos_financeiros ADD COLUMN corrente INTEGER NULL;

        UPDATE movimentos_financeiros SET corrente =
            CASE
                WHEN origem_modulo IN ('sale-payment', 'order-payment-caixa') THEN 2
                ELSE NULL
            END
        WHERE corrente IS NULL;
        """;
}
