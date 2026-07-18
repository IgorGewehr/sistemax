namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// Catálogo FIXO de contas de controle usado pelo motor de partida dobrada
/// (<see cref="LancamentoContabilFactory"/>). Enxuto de propósito — o público-alvo (PME
/// brasileira leiga) nunca vê nem edita isto; é o "circuit breaker" de integridade descrito em
/// docs/financeiro/financeiro-datamodel.md §1, opção C.
/// </summary>
public static class PlanoDeContasPadrao
{
    public static readonly ContaContabil CaixaEBancos = ContaContabil.Criar("1.1", "Caixa e Bancos", TipoContaContabil.Ativo);
    public static readonly ContaContabil ContasAReceber = ContaContabil.Criar("1.2", "Contas a Receber", TipoContaContabil.Ativo);
    public static readonly ContaContabil ContasAPagar = ContaContabil.Criar("2.1", "Contas a Pagar", TipoContaContabil.Passivo);
    public static readonly ContaContabil ImpostosARecolher = ContaContabil.Criar("2.2", "Impostos a Recolher", TipoContaContabil.Passivo);
    public static readonly ContaContabil Receita = ContaContabil.Criar("3.1", "Receita", TipoContaContabil.Receita);
    public static readonly ContaContabil CustoDespesa = ContaContabil.Criar("4.1", "Custo/Despesa", TipoContaContabil.Despesa);

    /// <summary>Análise por Projeto/Imobilizado (docs/financeiro/design-analise-por-projeto.md §4.4,
    /// docs/financeiro/design-imobilizado-roi.md §4.4) — a compra de um <c>AtivoDeCapital</c> (licença
    /// amortizável ou bem depreciável) nasce AQUI, nunca em 4.1: comprar capacidade é troca de ativo
    /// (balanço), a despesa só nasce com o uso do tempo (o cronograma de reconhecimento mensal, que
    /// credita esta conta e debita <see cref="CustoDespesa"/>).</summary>
    public static readonly ContaContabil AtivosDeCapital = ContaContabil.Criar("1.3", "Ativos de Capital", TipoContaContabil.Ativo);

    public static IReadOnlyCollection<ContaContabil> Todas { get; } =
    [
        CaixaEBancos, ContasAReceber, ContasAPagar, ImpostosARecolher, Receita, CustoDespesa, AtivosDeCapital
    ];
}
