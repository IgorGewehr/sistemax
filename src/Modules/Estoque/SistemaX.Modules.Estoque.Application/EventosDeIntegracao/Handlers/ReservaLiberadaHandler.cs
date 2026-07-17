using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>os.libera</c> → peça prevista e reservada, nunca aplicada (sobra de orçamento ou
/// cancelamento) — devolve ao disponível via <c>LiberacaoReserva</c>.
/// </summary>
public sealed class ReservaLiberadaHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos)
    : IIntegrationEventHandler<ReservaLiberada>
{
    public async Task HandleAsync(ReservaLiberada evento, CancellationToken ct = default)
    {
        var chave = $"os.libera:{evento.OrdemServicoId}:{evento.LinhaId}";
        if (await movimentos.ExisteComChaveAsync(chave, ct)) return;

        var produto = await produtos.ObterPorIdAsync(evento.ProdutoId, ct);
        if (produto is null || !produto.ControlaEstoque) return;

        var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, evento.ProdutoId, EstoqueConstantes.DepositoPadrao, ct);

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            evento.TenantId, EstoqueConstantes.DepositoPadrao, evento.ProdutoId, TipoMovimento.LiberacaoReserva,
            new Quantidade(evento.QuantidadeMilesimos), Money.Zero, new SourceRef("os", evento.OrdemServicoId), chave,
            $"Reserva liberada — OS {evento.OrdemServicoId} (não aplicada)", EstoqueConstantes.OperadorSistema,
            EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao liberar reserva da OS {evento.OrdemServicoId}: {movimentoResultado.Erro.Mensagem}");

        saldo.AplicarMovimento(movimentoResultado.Valor);
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);
        await saldos.SalvarAsync(saldo, ct);
    }
}
