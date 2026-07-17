namespace SistemaX.Modules.Financeiro.Application.Caixa;

/// <summary>
/// Interpretação da string livre de forma de pagamento que chega nos eventos de integração
/// (<c>VendaConcluida.FormaPagamento</c>, <c>PedidoPago.FormaPagamento</c> — ver
/// <c>SistemaX.Modules.Abstractions.IntegrationEvents</c>). Esses eventos carregam só um rótulo
/// de texto, não uma referência a uma <c>FormaDePagamento</c> cadastrada com prazo de compensação
/// próprio (docs/financeiro-datamodel.md §2.2).
///
/// SIMPLIFICAÇÃO DELIBERADA DO MVP, documentada explicitamente: dinheiro e PIX liquidam à vista
/// (gera <c>MovimentoFinanceiro</c> atômico); qualquer outra forma (cartão, boleto, fiado) vira
/// <c>ContaAReceber</c>/<c>ContaAPagar</c> a prazo com vencimento padrão de
/// <see cref="PrazoPadraoDiasAPrazo"/> dias. Fase 2: os eventos devem carregar o id da
/// <c>FormaDePagamento</c> cadastrada (com seu próprio prazo de compensação), não uma string.
/// </summary>
public static class ClassificadorFormaPagamento
{
    private static readonly HashSet<string> FormasAVista = new(StringComparer.OrdinalIgnoreCase) { "dinheiro", "pix" };

    public const int PrazoPadraoDiasAPrazo = 30;

    /// <summary>
    /// Id da conta-caixa "padrão" do tenant, usada quando o evento de integração não informa qual
    /// conta bancária/caixa recebeu o valor. Fase 2: resolver a conta-caixa real configurada por
    /// canal/forma de pagamento.
    /// </summary>
    public const string ContaCaixaPadraoId = "conta-caixa-padrao";

    public static bool EhAVista(string formaPagamento) => FormasAVista.Contains(formaPagamento.Trim());
}
