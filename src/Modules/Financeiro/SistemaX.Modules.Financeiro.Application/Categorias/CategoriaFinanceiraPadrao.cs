namespace SistemaX.Modules.Financeiro.Application.Categorias;

/// <summary>
/// Slugs de categoria pré-cadastrada usados pelos handlers de evento de integração para
/// classificar o fato financeiro que criam. SIMPLIFICAÇÃO DELIBERADA DO MVP: o modelo-alvo
/// (docs/financeiro-features.md §4.11) tem <c>Categoria</c> como entidade por-tenant, resolvida
/// via <c>ICategoriaRepository.BuscarPorSlugAsync(businessId, slug)</c> — aqui usamos o slug
/// diretamente como <c>CategoriaId</c> porque o catálogo de handlers do MVP não depende de
/// cadastro prévio por tenant para compilar/testar a lógica financeira em si. Trocar por
/// resolução real de <c>Categoria</c> é um passo de Fase 2 que não muda a forma dos handlers.
/// </summary>
public static class CategoriaFinanceiraPadrao
{
    public const string Servicos = "servicos";
    public const string Comissoes = "comissoes";
    public const string CustoMercadoriaVendida = "cmv-fornecedor";
    public const string Delivery = "delivery";
    public const string DespesaComPessoal = "despesa-com-pessoal";
    public const string EstornoVenda = "estorno-venda";

    /// <summary>
    /// Receita de assinatura (MRR) — primeiro passo, aditivo, em direção à dimensão "corrente de
    /// receita" (docs/financeiro/revisao-domain-fit-cnpj.md P0-1: recorrente × serviço × comércio).
    /// Por ora só distingue assinatura do resto — venda/OS continuam em <see cref="Servicos"/> até
    /// a corrente completa (enum <c>CorrenteReceita</c> propagado por evento) ser construída.
    /// Não participa de nenhum filtro de custo/despesa do DRE (só a receita usa esta categoria),
    /// então introduzi-la é seguro: não muda nenhum número já calculado, só habilita a quebra
    /// receita recorrente × receita operacional em <see cref="ReadModels.DreGerencialService"/>.
    /// </summary>
    public const string ReceitaRecorrente = "receita-recorrente";

    /// <summary>
    /// P1-6 (docs/financeiro/revisao-domain-fit-cnpj.md) — rótulo da linha "despesas financeiras
    /// (MDR)" que <see cref="ReadModels.DreGerencialService"/> devolve. Não é <c>CategoriaId</c> de
    /// nenhuma <c>ContaAPagar</c> — o MDR é derivado AO VIVO de <c>fato_recebiveis</c> (Σ bruto −
    /// líquido do período, já calculado pelo lar único <c>FormaDePagamento</c>, nunca recomputado
    /// em paralelo), então esta constante só nomeia a linha para a UI/relatórios.
    /// </summary>
    public const string TaxasDeCartao = "taxas-de-cartao";
}
