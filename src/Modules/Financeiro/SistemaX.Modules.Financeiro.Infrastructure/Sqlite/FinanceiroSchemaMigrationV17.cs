using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v17 do módulo "financeiro" — espelha <see cref="FinanceiroSchemaMigrationV16"/> para
/// <c>ContaAPagar</c> (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md): coluna <c>corrente</c>
/// NULLABLE + backfill. <c>ContaAPagar</c> não tem <c>SourceRef.Modulo</c> tão discriminante quanto
/// venda/OS/assinatura (comissão e CMV de compra ainda nascem só via <c>LancarContaUseCase</c>/
/// <c>CompraRecebidaHandler</c>, sem tagging explícito de corrente na origem) — o backfill usa a
/// MESMA regra de <c>CorrenteDeReceitaInferencia.InferirDaCategoria</c> (comissão ⇒ Servico, CMV de
/// fornecedor ⇒ Comercio) para as duas nunca divergirem silenciosamente.
/// </summary>
public sealed class FinanceiroSchemaMigrationV17 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 17;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_pagar ADD COLUMN corrente INTEGER NULL;

        UPDATE contas_a_pagar SET corrente =
            CASE
                WHEN categoria_id = 'comissoes' THEN 1
                WHEN categoria_id = 'cmv-fornecedor' THEN 2
                ELSE NULL
            END
        WHERE corrente IS NULL;
        """;
}
