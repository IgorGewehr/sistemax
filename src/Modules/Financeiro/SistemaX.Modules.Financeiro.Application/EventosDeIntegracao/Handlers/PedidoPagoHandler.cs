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
/// <c>pedido.pago</c> — pagamento de pedido (delivery/balcão) já confirmado pelo gateway: a
/// <c>ContaAReceber</c> nasce JÁ QUITADA, e o <c>MovimentoFinanceiro</c> nasce no mesmo instante
/// (docs/financeiro-datamodel.md §4.2 — "pagamento online é sempre resolvido no evento").
///
/// SIMPLIFICAÇÃO DO MVP: a spec recomenda a chave composta <c>{orderId}_{gatewayPaymentId}</c>
/// para dedupar contra retry de webhook do PSP, mas <see cref="PedidoPago"/>
/// (SistemaX.Modules.Abstractions.IntegrationEvents) não carrega um id de pagamento do gateway
/// separado — usamos <c>PedidoId</c> sozinho. Se o catálogo de eventos ganhar esse campo, trocar
/// a chave aqui sem mudar a lógica do handler.
/// </summary>
public sealed class PedidoPagoHandler(
    IContaAReceberRepository contasAReceber,
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos) : IIntegrationEventHandler<PedidoPago>
{
    public async Task HandleAsync(PedidoPago evento, CancellationToken ct = default)
    {
        var origem = new SourceRef("order-payment", evento.PedidoId);
        if (await contasAReceber.BuscarPorOrigemAsync(evento.TenantId, origem.Chave, ct) is not null)
            return;

        var valor = new Money(evento.TotalCentavos);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, evento.OcorridoEm);

        // Corrente: pedido (delivery/balcão) é venda de produto — Comercio (P0-1).
        var contaResultado = ContaAReceber.Criar(
            evento.TenantId, origem, $"Pedido {evento.PedidoId}", CategoriaFinanceiraPadrao.Delivery, evento.OcorridoEm, valor, parcelas,
            corrente: CorrenteDeReceita.Comercio);
        if (contaResultado.Falha)
            throw new InvalidOperationException($"Falha ao criar ContaAReceber para pedido {evento.PedidoId}: {contaResultado.Erro.Mensagem}");

        var conta = contaResultado.Valor;
        var parcela = conta.Parcelas[0];

        var liquidacao = conta.RegistrarLiquidacaoParcela(parcela.Id, valor, evento.OcorridoEm, evento.FormaPagamento);
        if (liquidacao.Falha)
            throw new InvalidOperationException($"Falha ao liquidar parcela do pedido {evento.PedidoId}: {liquidacao.Erro.Mensagem}");

        await contasAReceber.SalvarAsync(conta, ct);

        var lancamentoCompetencia = LancamentoContabilFactory.DeContaAReceber(conta);
        if (lancamentoCompetencia.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil de competência do pedido {evento.PedidoId}: {lancamentoCompetencia.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCompetencia.Valor, ct);

        var movimentoResultado = MovimentoFinanceiro.Registrar(
            evento.TenantId, ClassificadorFormaPagamento.ContaCaixaPadraoId, evento.FormaPagamento, parcela.Id,
            conta.Id, TipoMovimentoFinanceiro.Entrada, valor, evento.OcorridoEm, new SourceRef("order-payment-caixa", evento.PedidoId),
            corrente: CorrenteDeReceita.Comercio);
        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao registrar movimento de caixa do pedido {evento.PedidoId}: {movimentoResultado.Erro.Mensagem}");
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);

        var lancamentoCaixa = LancamentoContabilFactory.DeMovimento(movimentoResultado.Valor);
        if (lancamentoCaixa.Falha)
            throw new InvalidOperationException($"Falha ao gerar lançamento contábil de caixa do pedido {evento.PedidoId}: {lancamentoCaixa.Erro.Mensagem}");
        await lancamentos.SalvarAsync(lancamentoCaixa.Valor, ct);
    }
}
