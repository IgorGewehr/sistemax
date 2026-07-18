using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.Caixa;

/// <summary>
/// LAR ÚNICO de "essa forma de pagamento é à vista, e se não for, em quantos dias compensa?" —
/// P2-7 (docs/financeiro/revisao-domain-fit-cnpj.md): antes, <see cref="ClassificadorFormaPagamento"/>
/// tinha uma regra BINÁRIA própria (dinheiro/pix à vista; QUALQUER outra forma vira D+30 fixo) que
/// divergia silenciosamente da <c>FormaDePagamento</c> CADASTRADA que <c>FatoRecebiveisProjection</c>
/// já consulta (débito D+1, boleto D+2, crédito D+30 — via
/// <see cref="IFormaDePagamentoRepository.ObterPorNomeAsync"/>). Dois "lares" do mesmo prazo podiam
/// divergir: uma venda no débito nascia com <c>ContaAReceber</c> vencendo em D+30 (heurística velha)
/// mas <c>fato_recebiveis</c> já sabia que débito liquida em D+1 — o recebível ficava "Atrasado" por
/// quase um mês antes do dinheiro sequer estar previsto pra cair.
///
/// AGORA: resolve contra a <c>FormaDePagamento</c> cadastrada do tenant primeiro — <c>PrazoCompensacaoDias
/// == 0</c> é à vista, qualquer outro valor é o prazo real cadastrado. Só cai no fallback antigo
/// (<see cref="ClassificadorFormaPagamento"/>: dinheiro/pix à vista, resto D+30) quando a forma NÃO
/// está cadastrada — nunca inventa um prazo que o tenant não configurou, mesmo racional conservador
/// de <c>FatoRecebiveisProjection.ResolverTaxaELagAsync</c>.
/// </summary>
public sealed class ResolvedorDePrazoDeCompensacao(IFormaDePagamentoRepository formasDePagamento)
{
    public async Task<(bool EhAVista, int PrazoDias)> ResolverAsync(
        string businessId, string formaPagamento, CancellationToken ct = default)
    {
        var forma = await formasDePagamento.ObterPorNomeAsync(businessId, formaPagamento, ct).ConfigureAwait(false);
        if (forma is not null)
        {
            return (forma.PrazoCompensacaoDias <= 0, forma.PrazoCompensacaoDias);
        }

        var ehAVista = ClassificadorFormaPagamento.EhAVista(formaPagamento);
        return (ehAVista, ehAVista ? 0 : ClassificadorFormaPagamento.PrazoPadraoDiasAPrazo);
    }
}
