using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Caixa;

/// <summary>
/// LAR ÚNICO de "resolver o valor líquido de MDR quando a forma de pagamento é conhecida" para
/// leitores que iteram MUITAS parcelas (projeções de fluxo de caixa) — evita reconsultar
/// <see cref="IFormaDePagamentoRepository"/> uma vez por parcela quando várias compartilham a
/// mesma forma (<paramref name="cache"/> por chamada). Mesma regra de <see cref="FormaDePagamento.CalcularValorLiquido"/>
/// que <c>BaixarParcelaUseCase</c>/<c>FatoRecebiveisProjection</c> usam — nunca uma fórmula paralela.
///
/// FALLBACK CONSERVADOR (mesmo padrão do resto do módulo): forma nula (parcela ainda nunca paga —
/// <c>Parcela.FormaPagamentoId</c> só nasce em <c>RegistrarPagamento</c>, docs/financeiro/revisao-domain-fit-cnpj.md
/// P1-6) ou forma não cadastrada → devolve o BRUTO, nunca inventa desconto que o tenant não
/// configurou.
/// </summary>
public static class ResolvedorDeValorLiquido
{
    public static async Task<Money> ResolverAsync(
        IFormaDePagamentoRepository formasDePagamento, string businessId, string? formaPagamentoId,
        Money valorBruto, IDictionary<string, FormaDePagamento?> cache, CancellationToken ct = default)
    {
        if (formaPagamentoId is null) return valorBruto;

        if (!cache.TryGetValue(formaPagamentoId, out var forma))
        {
            forma = await formasDePagamento.ObterPorNomeAsync(businessId, formaPagamentoId, ct).ConfigureAwait(false);
            cache[formaPagamentoId] = forma;
        }

        return forma?.CalcularValorLiquido(valorBruto) ?? valorBruto;
    }
}
