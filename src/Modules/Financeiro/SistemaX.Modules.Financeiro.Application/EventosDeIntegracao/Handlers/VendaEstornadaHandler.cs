using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>venda.estornada</c> — dois caminhos, nunca um DELETE (docs/financeiro-datamodel.md §4.4):
/// (a) conta ainda ABERTA (nada pago) → cancela em FSM, anulação pura, sem caixa a reverter;
/// (b) conta já PAGA/PARCIAL → gera um MovimentoFinanceiro + LancamentoContabil de ESTORNO
/// (sinal invertido, <c>ReversalOfId</c> apontando pro original), o original nunca é tocado.
/// Idempotente por <c>SourceRef("sale-reversal", VendaId)</c>.
/// </summary>
public sealed class VendaEstornadaHandler(
    IContaAReceberRepository contasAReceber,
    IMovimentoFinanceiroRepository movimentos,
    EstornarMovimentoUseCase estornarMovimento) : IIntegrationEventHandler<VendaEstornada>
{
    public async Task HandleAsync(VendaEstornada evento, CancellationToken ct = default)
    {
        var origemVenda = new SourceRef("sale", evento.VendaId);
        var conta = await contasAReceber.BuscarPorOrigemAsync(evento.TenantId, origemVenda.Chave, ct);
        if (conta is null)
            throw new InvalidOperationException(
                $"Venda {evento.VendaId} estornada mas nenhuma ContaAReceber correspondente foi encontrada — verifique se venda.concluida já foi processado antes deste evento.");

        if (conta.Status is StatusFinanceiro.Cancelado)
            return; // já cancelada por um replay anterior deste mesmo evento — idempotência

        if (conta.Status is StatusFinanceiro.Aberto)
        {
            var cancelamento = conta.Cancelar($"Venda {evento.VendaId} estornada antes de qualquer pagamento");
            if (cancelamento.Falha)
                throw new InvalidOperationException($"Falha ao cancelar ContaAReceber da venda {evento.VendaId}: {cancelamento.Erro.Mensagem}");

            await contasAReceber.SalvarAsync(conta, ct);
            return;
        }

        // Conta já tem pagamento registrado (Parcial/Pago/Atrasado com parcela paga) — estorna o
        // MovimentoFinanceiro original de entrada, nunca cancela a conta em si (fato imutável).
        var movimentoOriginal = await movimentos.BuscarPorOrigemAsync(evento.TenantId, new SourceRef("sale-payment", evento.VendaId).Chave, ct);
        if (movimentoOriginal is null)
            throw new InvalidOperationException(
                $"Venda {evento.VendaId} tem parcela paga mas nenhum MovimentoFinanceiro de origem 'sale-payment' foi encontrado — dado inconsistente.");

        var origemEstorno = new SourceRef("sale-reversal", evento.VendaId);
        var estornoResultado = await estornarMovimento.ExecutarAsync(
            movimentoOriginal.Id, evento.OcorridoEm, origemEstorno, $"Venda {evento.VendaId} estornada", ct);

        if (estornoResultado.Falha)
            throw new InvalidOperationException($"Falha ao estornar movimento da venda {evento.VendaId}: {estornoResultado.Erro.Mensagem}");
    }
}
