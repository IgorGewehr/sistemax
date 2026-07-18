using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>venda.concluida</c> → cria <see cref="ContaAReceber"/> (+ <c>Parcela</c>) e, se a forma de
/// pagamento liquida à vista, o <see cref="MovimentoFinanceiro"/> atômico correspondente — os
/// dois fatos nascem no mesmo instante para venda à vista (docs/financeiro-datamodel.md §3).
/// Idempotente por <c>SourceRef("sale", VendaId)</c> — reprocessar o mesmo evento é no-op.
///
/// P2-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — à-vista/a-prazo e o prazo de vencimento
/// resolvem contra a <c>FormaDePagamento</c> CADASTRADA do tenant via
/// <see cref="ResolvedorDePrazoDeCompensacao"/> (LAR único com <c>FatoRecebiveisProjection</c>),
/// não mais a heurística binária própria (dinheiro/pix à vista, resto D+30 fixo) que podia divergir
/// do prazo real (débito D+1, boleto D+2). Fallback para essa heurística só quando a forma não está
/// cadastrada (mesmo racional conservador de sempre).
/// </summary>
public sealed class VendaConcluidaHandler(
    IContaAReceberRepository contasAReceber,
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos,
    ResolvedorDePrazoDeCompensacao resolvedorDePrazo) : IIntegrationEventHandler<VendaConcluida>
{
    public async Task HandleAsync(VendaConcluida evento, CancellationToken ct = default)
    {
        var origemVenda = new SourceRef("sale", evento.VendaId);
        if (await contasAReceber.BuscarPorOrigemAsync(evento.TenantId, origemVenda.Chave, ct) is not null)
            return; // replay do mesmo evento — idempotência

        var valor = new Money(evento.TotalCentavos);
        var (ehAVista, prazoDias) = await resolvedorDePrazo.ResolverAsync(evento.TenantId, evento.FormaPagamento, ct).ConfigureAwait(false);
        var vencimento = ehAVista ? evento.OcorridoEm : evento.OcorridoEm.AddDays(prazoDias);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, vencimento);

        // Corrente: venda de balcão/produto é sempre Comercio (P0-1) — VendaConcluida hoje só
        // representa venda avulsa de peça/produto, nunca OS (que tem seu próprio evento/handler).
        var contaResultado = ContaAReceber.Criar(
            evento.TenantId, origemVenda, $"Venda {evento.VendaId}", CategoriaFinanceiraPadrao.Servicos, evento.OcorridoEm, valor, parcelas,
            corrente: CorrenteDeReceita.Comercio);
        if (contaResultado.Falha)
            throw new InvalidOperationException($"Falha ao criar ContaAReceber para venda {evento.VendaId}: {contaResultado.Erro.Mensagem}");

        var conta = contaResultado.Valor;
        var parcela = conta.Parcelas[0];

        if (ehAVista)
        {
            var liquidacao = conta.RegistrarLiquidacaoParcela(parcela.Id, valor, evento.OcorridoEm, evento.FormaPagamento);
            if (liquidacao.Falha)
                throw new InvalidOperationException($"Falha ao liquidar parcela à vista da venda {evento.VendaId}: {liquidacao.Erro.Mensagem}");
        }

        await contasAReceber.SalvarAsync(conta, ct);

        var lancamentoCompetencia = LancamentoContabilFactory.DeContaAReceber(conta);
        if (lancamentoCompetencia.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil de competência da venda {evento.VendaId}: {lancamentoCompetencia.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCompetencia.Valor, ct);

        if (!ehAVista) return;

        var movimentoResultado = MovimentoFinanceiro.Registrar(
            evento.TenantId, ClassificadorFormaPagamento.ContaCaixaPadraoId, evento.FormaPagamento, parcela.Id,
            conta.Id, TipoMovimentoFinanceiro.Entrada, valor, evento.OcorridoEm, new SourceRef("sale-payment", evento.VendaId),
            corrente: CorrenteDeReceita.Comercio);
        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao registrar movimento de caixa da venda {evento.VendaId}: {movimentoResultado.Erro.Mensagem}");
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);

        var lancamentoCaixa = LancamentoContabilFactory.DeMovimento(movimentoResultado.Valor);
        if (lancamentoCaixa.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil de caixa da venda {evento.VendaId}: {lancamentoCaixa.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCaixa.Valor, ct);
    }
}
