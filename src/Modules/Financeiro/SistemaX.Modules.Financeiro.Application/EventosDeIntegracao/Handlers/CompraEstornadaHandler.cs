using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>compra.estornada</c> — fecha o GAP DOCUMENTADO em
/// <c>SistemaX.Modules.Abstractions.IntegrationEvents.CompraEstornada</c>: mesmos dois caminhos de
/// <see cref="VendaEstornadaHandler"/>, nunca um DELETE (docs/financeiro-datamodel.md §4.4) — (a)
/// <c>ContaAPagar</c> ainda ABERTA (nada pago) → cancela em FSM, anulação pura; (b) conta já
/// PAGA/PARCIAL (baixada manualmente via <c>BaixarParcelaUseCase</c> antes do estorno chegar) →
/// gera um <c>MovimentoFinanceiro</c> + <c>LancamentoContabil</c> de ESTORNO por movimento
/// original, o original nunca é tocado.
///
/// DIFERENÇA DELIBERADA de <see cref="VendaEstornadaHandler"/>: uma compra nunca tem pagamento À
/// VISTA automático (só <c>ContaRecebidaHandler</c> gera a prazo — ver <c>CompraRecebidaHandler</c>),
/// então não existe <c>SourceRef("purchase-payment", CompraId)</c> deterministicamente previsível.
/// O pagamento só pode ter vindo de uma baixa MANUAL (<c>BaixarParcelaUseCase</c>), cuja
/// <c>SourceRef</c> é opaca (chave de idempotência do CHAMADOR) — por isso a busca é por
/// <see cref="IMovimentoFinanceiroRepository.ListarPorContaOrigemAsync"/> (o id da própria conta),
/// não por origem. Idempotente por <c>SourceRef("purchaseNote", CompraId)</c> pra achar a conta e
/// por <c>EstornarMovimentoUseCase</c> (idempotente por movimento original) pra cada estorno.
/// </summary>
public sealed class CompraEstornadaHandler(
    IContaAPagarRepository contasAPagar,
    IMovimentoFinanceiroRepository movimentos,
    EstornarMovimentoUseCase estornarMovimento) : IIntegrationEventHandler<CompraEstornada>
{
    public async Task HandleAsync(CompraEstornada evento, CancellationToken ct = default)
    {
        var origemCompra = new SourceRef("purchaseNote", evento.CompraId);
        var conta = await contasAPagar.BuscarPorOrigemAsync(evento.TenantId, origemCompra.Chave, ct);
        if (conta is null)
            throw new InvalidOperationException(
                $"Compra {evento.CompraId} estornada mas nenhuma ContaAPagar correspondente foi encontrada — verifique se compra.recebida já foi processado antes deste evento.");

        if (conta.Status is StatusFinanceiro.Cancelado)
            return; // já cancelada por um replay anterior deste mesmo evento — idempotência

        if (conta.Status is StatusFinanceiro.Aberto)
        {
            var cancelamento = conta.Cancelar($"Compra {evento.CompraId} estornada antes de qualquer pagamento");
            if (cancelamento.Falha)
                throw new InvalidOperationException($"Falha ao cancelar ContaAPagar da compra {evento.CompraId}: {cancelamento.Erro.Mensagem}");

            await contasAPagar.SalvarAsync(conta, ct);
            return;
        }

        // Conta já tem pagamento registrado (Parcial/Pago/Atrasado com parcela paga via baixa
        // manual) — estorna cada MovimentoFinanceiro original ligado à conta, nunca cancela a conta
        // em si (fato imutável). EstornarMovimentoUseCase já é idempotente por movimento original.
        var movimentosDaConta = await movimentos.ListarPorContaOrigemAsync(evento.TenantId, conta.Id, ct);
        var originais = movimentosDaConta.Where(m => !m.EhEstorno).ToList();
        if (originais.Count == 0)
            throw new InvalidOperationException(
                $"Compra {evento.CompraId} tem parcela paga mas nenhum MovimentoFinanceiro ligado à conta '{conta.Id}' foi encontrado — dado inconsistente.");

        foreach (var original in originais)
        {
            var origemEstorno = new SourceRef("purchase-reversal", $"{evento.CompraId}:{original.Id}");
            var estornoResultado = await estornarMovimento.ExecutarAsync(
                original.Id, evento.OcorridoEm, origemEstorno, $"Compra {evento.CompraId} estornada", ct);

            if (estornoResultado.Falha)
                throw new InvalidOperationException($"Falha ao estornar movimento '{original.Id}' da compra {evento.CompraId}: {estornoResultado.Erro.Mensagem}");
        }
    }
}
