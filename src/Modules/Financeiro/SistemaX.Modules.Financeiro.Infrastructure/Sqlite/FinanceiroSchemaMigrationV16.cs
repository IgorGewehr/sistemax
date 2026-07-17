using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Modules.Financeiro.Infrastructure.Sqlite;

/// <summary>
/// Migração v16 do módulo "financeiro" — dimensão "corrente de receita" em <c>ContaAReceber</c>
/// (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md). Coluna <c>corrente</c> NULLABLE (INTEGER,
/// valores pinados de <c>CorrenteDeReceita</c>: 0=Recorrente, 1=Servico, 2=Comercio — nunca
/// reordenar) + backfill RETROCOMPATÍVEL do dado já gravado.
///
/// BACKFILL: linhas antigas não têm a coluna preenchida pelo domínio (ela só passou a ser gravada
/// a partir desta migração); inferimos pela mesma pista forte que os handlers usavam para decidir
/// <c>SourceRef.Modulo</c> — venda avulsa (<c>"sale"</c>) e pedido (<c>"order-payment"</c>) são
/// sempre Comercio, OS (<c>"appointment"</c>) é sempre Servico, assinatura (<c>"assinatura"</c>) é
/// sempre Recorrente. Onde o módulo de origem não distingue por si (ex.: <c>"recorrencia"</c>,
/// template genérico de conta a pagar/receber), caímos para a MESMA regra de
/// <c>CorrenteDeReceitaInferencia.InferirDaCategoria</c> (categoria <c>receita-recorrente</c> ⇒
/// Recorrente) — as duas NUNCA devem divergir silenciosamente. Qualquer linha que não bata em
/// nenhum caso fica <c>NULL</c> (não classificada nesta dimensão) em vez de arriscar um palpite
/// errado — o DRE trata <c>Corrente == null</c> como "fora da quebra por corrente", nunca como erro.
/// </summary>
public sealed class FinanceiroSchemaMigrationV16 : SqlModuleSchemaMigration
{
    public override string Modulo => "financeiro";

    public override int Versao => 16;

    protected override string Sql =>
        """
        ALTER TABLE contas_a_receber ADD COLUMN corrente INTEGER NULL;

        UPDATE contas_a_receber SET corrente =
            CASE
                WHEN source_ref_modulo IN ('sale', 'order-payment') THEN 2
                WHEN source_ref_modulo = 'appointment' THEN 1
                WHEN source_ref_modulo = 'assinatura' THEN 0
                WHEN categoria_id = 'receita-recorrente' THEN 0
                ELSE NULL
            END
        WHERE corrente IS NULL;
        """;
}
