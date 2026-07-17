namespace SistemaX.Verticals.Assistencia;

/// <summary>Forma de pagamento recebida na entrega (a OS fatura quando o cliente retira — não
/// existe "a prazo" no MVP, ver §7.5 do plano). Tipo próprio do vertical (não reaproveita
/// <c>MetodoPagamento</c> de Vendas.Domain — verticais não dependem de outros módulos além de
/// Abstractions/SharedKernel).</summary>
public enum FormaPagamento
{
    Dinheiro,
    Pix,
    CartaoDebito,
    CartaoCredito
}

public static class FormaPagamentoExtensions
{
    /// <summary>
    /// Tradução na FRONTEIRA (P1-7, docs/financeiro/revisao-domain-fit-cnpj.md) para o vocabulário
    /// que o Financeiro já usa em <c>FormaDePagamento</c>/<c>FinanceiroBootstrapSeeder</c>
    /// (<c>"dinheiro"</c>/<c>"pix"</c>/<c>"debito"</c>/<c>"credito"</c>) — sem esta normalização
    /// <c>"CartaoDebito".ToString()</c> nunca bateria com a <c>FormaDePagamento</c> cadastrada
    /// (resolução é por nome EXATO case-insensitive, nunca substring) e MDR/lag do recebível de OS
    /// cairiam sempre no fallback conservador de forma desconhecida.
    /// </summary>
    public static string ParaChaveFinanceira(this FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro => "dinheiro",
        FormaPagamento.Pix => "pix",
        FormaPagamento.CartaoDebito => "debito",
        FormaPagamento.CartaoCredito => "credito",
        _ => forma.ToString()
    };
}
