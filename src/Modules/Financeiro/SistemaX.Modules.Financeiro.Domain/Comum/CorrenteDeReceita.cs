namespace SistemaX.Modules.Financeiro.Domain.Comum;

/// <summary>
/// Dimensão "corrente de receita" (P0-1, docs/financeiro/revisao-domain-fit-cnpj.md) — o CNPJ-alvo
/// (assistência técnica) opera três correntes com economia unitária e tributação diferentes, hoje
/// todas colapsadas num DRE de balde único:
/// <list type="bullet">
/// <item><see cref="Recorrente"/> — MRR de <c>Assinatura</c>: MC ~100%, sem CMV.</item>
/// <item><see cref="Servico"/> — Ordem de Serviço (mão de obra + peças aplicadas): MC alta, custo
/// direto é comissão do técnico (e, quando P0-5 estiver pronto, o custo da peça consumida).</item>
/// <item><see cref="Comercio"/> — venda avulsa de peça/produto no balcão/delivery: MC baixa,
/// CMV real via <c>CustoBaixadoPorVenda</c>.</item>
/// </list>
///
/// VALORES PINADOS (nunca reordenar os membros): esta enum é persistida como <c>INTEGER</c> em
/// <c>contas_a_receber.corrente</c>/<c>contas_a_pagar.corrente</c>/<c>movimentos_financeiros.corrente</c>/
/// <c>fato_receita_diaria.corrente</c>/<c>fato_custo_diario.corrente</c> (ver
/// <c>FinanceiroSchemaMigrationV16</c>) — mudar a ordem declarada corrompe silenciosamente todo
/// dado já gravado.
/// </summary>
public enum CorrenteDeReceita
{
    Recorrente = 0,
    Servico = 1,
    Comercio = 2,
}
