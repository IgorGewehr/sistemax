namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fact table por PRODUTO (catálogo #6 do plano de inteligência do Financeiro — docs/financeiro/
/// inteligencia-arquitetura.md §4/ADR-0005) — receita, CMV e margem de contribuição, foldados do
/// ledger. É o "motor base" que quase todas as análises da F1 dependem (breakeven, radar do
/// Simples usam o CMV/MC daqui indiretamente). <see cref="Dia"/> já vem bucketado no fuso do
/// tenant (ver <c>BucketingTemporalDoTenant</c>).
///
/// CMV via <c>CustoBaixadoPorVenda</c> — mas esse evento só carrega o custo TOTAL da venda, nunca
/// por produto (o Estoque soma o custo de todas as linhas antes de publicar). O fold
/// (<see cref="FatoMargemProdutoProjection"/>) resolve isso com RATEIO PROPORCIONAL à receita de
/// cada item dentro da mesma venda (<see cref="Quant.RateioProporcional"/>) — uma aproximação
/// documentada, não uma alocação exata por linha (que exigiria o Estoque publicar custo por item,
/// fora de escopo da F1). <see cref="MargemContribuicaoCentavos"/> é sempre DERIVADA (receita −
/// custo), nunca armazenada — mesmo princípio de <c>FatoCaixaDiario.SaldoDiaCentavos</c>.
///
/// LIMITAÇÃO CONHECIDA DA F1 (documentada, não escondida): não reage a <c>VendaEstornada</c> — o
/// estorno reduz <c>fato_receita_diaria</c>/<c>fato_custo_diario</c> agregados (F0), mas não este
/// fold por produto, porque a granularidade por item já foi descartada no momento em que o custo é
/// alocado (ver <see cref="FatoMargemProdutoProjection"/>). Fica documentado como follow-up: quando
/// <c>VendaEstornada</c> carregar a mesma quebra por item de <c>VendaItensMovimentados</c>, este
/// fold ganha o handler simétrico.
/// </summary>
public sealed record FatoMargemProduto(
    string TenantId, string ProdutoId, DateOnly Dia, long ReceitaCentavos, long CustoCentavos, DateTimeOffset AtualizadoEmUtc)
{
    public long MargemContribuicaoCentavos => ReceitaCentavos - CustoCentavos;
}
