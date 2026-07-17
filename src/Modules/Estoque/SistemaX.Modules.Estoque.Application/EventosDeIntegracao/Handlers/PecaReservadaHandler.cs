using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>os.reserva</c> → <c>Reserva</c> (físico intacto, reservado sobe). Reserva NUNCA bloqueia a
/// OS — política fixada no plano da OS: se <c>Disponivel</c> ficar negativo após a reserva, o
/// movimento é gravado do mesmo jeito e o Estoque publica <c>ReservaDescoberta</c> (quem assina
/// mostra o aviso; a OS segue seu fluxo normal).
/// </summary>
public sealed class PecaReservadaHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos, IIntegrationEventBus bus)
    : IIntegrationEventHandler<PecaReservada>
{
    public async Task HandleAsync(PecaReservada evento, CancellationToken ct = default)
    {
        var chave = $"os.reserva:{evento.OrdemServicoId}:{evento.LinhaId}";
        if (await movimentos.ExisteComChaveAsync(chave, ct)) return;

        var produto = await produtos.ObterPorIdAsync(evento.ProdutoId, ct);
        if (produto is null || !produto.ControlaEstoque) return;

        var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, evento.ProdutoId, EstoqueConstantes.DepositoPadrao, ct);

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            evento.TenantId, EstoqueConstantes.DepositoPadrao, evento.ProdutoId, TipoMovimento.Reserva,
            new Quantidade(evento.QuantidadeMilesimos), Money.Zero, new SourceRef("os", evento.OrdemServicoId), chave,
            $"Reserva — OS {evento.OrdemServicoId}", EstoqueConstantes.OperadorSistema, EstoqueConstantes.OperadorSistemaNome,
            evento.OcorridoEm);

        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao reservar peça da OS {evento.OrdemServicoId}: {movimentoResultado.Erro.Mensagem}");

        var movimento = movimentoResultado.Valor;
        saldo.AplicarMovimento(movimento);

        await movimentos.SalvarAsync(movimento, ct);
        await saldos.SalvarAsync(saldo, ct);

        if (saldo.Disponivel.EhNegativa)
        {
            await bus.PublishAsync(new ReservaDescoberta(
                evento.OrdemServicoId, evento.TenantId, evento.LinhaId, evento.ProdutoId,
                -saldo.Disponivel.Milesimos, evento.OcorridoEm), ct);
        }
    }
}
