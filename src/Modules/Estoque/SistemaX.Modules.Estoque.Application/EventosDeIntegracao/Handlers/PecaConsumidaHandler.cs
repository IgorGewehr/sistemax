using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>os.baixa</c> → libera a reserva da mesma linha (se houver) e baixa o físico, na mesma
/// operação lógica. Peça extra (sem reserva prévia — <c>PecaOrcada</c> nunca criada) só gera
/// <c>Saida</c>. <see cref="PecaConsumida.PrecoUnitarioCentavos"/> é preço de VENDA — o Estoque
/// ignora esse valor para custo (baixa pelo custo médio vigente); ele existe só para quem quiser
/// cruzar com margem depois.
///
/// P0-5 (docs/financeiro/revisao-domain-fit-cnpj.md): ao final da baixa, publica
/// <see cref="CustoBaixadoPorOs"/> com o CMV real desta linha (custo médio vigente NO INSTANTE DA
/// BAIXA × quantidade — capturado ANTES de <c>saldo.AplicarMovimento</c>, mesmo racional de
/// <c>VendaItensMovimentadosHandler</c>) — sem isso o <c>fato_custo_diario</c> nunca recebia o CMV
/// da corrente Servico e a margem por OS ficava incomputável mesmo com <c>OsFaturada</c> ligada.
/// </summary>
public sealed class PecaConsumidaHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos, IIntegrationEventBus bus)
    : IIntegrationEventHandler<PecaConsumida>
{
    public async Task HandleAsync(PecaConsumida evento, CancellationToken ct = default)
    {
        var chaveBaixa = $"os.baixa:{evento.OrdemServicoId}:{evento.LinhaId}";
        if (await movimentos.ExisteComChaveAsync(chaveBaixa, ct)) return;

        var produto = await produtos.ObterPorIdAsync(evento.ProdutoId, ct);
        if (produto is null || !produto.ControlaEstoque) return;

        var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, evento.ProdutoId, EstoqueConstantes.DepositoPadrao, ct);
        var quantidade = new Quantidade(evento.QuantidadeMilesimos);

        var chaveReserva = $"os.reserva:{evento.OrdemServicoId}:{evento.LinhaId}";
        var chaveLiberacaoPorConsumo = $"os.libera-consumo:{evento.OrdemServicoId}:{evento.LinhaId}";
        var haviaReservaAindaAberta = await movimentos.ExisteComChaveAsync(chaveReserva, ct)
                                       && !await movimentos.ExisteComChaveAsync(chaveLiberacaoPorConsumo, ct);

        if (haviaReservaAindaAberta)
        {
            var liberacaoResultado = MovimentoDeEstoque.Registrar(
                evento.TenantId, EstoqueConstantes.DepositoPadrao, evento.ProdutoId, TipoMovimento.LiberacaoReserva,
                quantidade, Money.Zero, new SourceRef("os", evento.OrdemServicoId), chaveLiberacaoPorConsumo,
                $"Consumo aplicado — libera reserva da OS {evento.OrdemServicoId}", EstoqueConstantes.OperadorSistema,
                EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

            if (liberacaoResultado.Falha)
                throw new InvalidOperationException($"Falha ao liberar reserva por consumo da OS {evento.OrdemServicoId}: {liberacaoResultado.Erro.Mensagem}");

            saldo.AplicarMovimento(liberacaoResultado.Valor);
            await movimentos.SalvarAsync(liberacaoResultado.Valor, ct);
        }

        var custoUnitarioNoInstanteDaBaixa = saldo.CustoMedio;

        var baixaResultado = MovimentoDeEstoque.Registrar(
            evento.TenantId, EstoqueConstantes.DepositoPadrao, evento.ProdutoId, TipoMovimento.Saida, quantidade,
            custoUnitarioNoInstanteDaBaixa, new SourceRef("os", evento.OrdemServicoId), chaveBaixa, $"Consumo — OS {evento.OrdemServicoId}",
            EstoqueConstantes.OperadorSistema, EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

        if (baixaResultado.Falha)
            throw new InvalidOperationException($"Falha ao baixar consumo da OS {evento.OrdemServicoId}: {baixaResultado.Erro.Mensagem}");

        saldo.AplicarMovimento(baixaResultado.Valor);
        await movimentos.SalvarAsync(baixaResultado.Valor, ct);
        await saldos.SalvarAsync(saldo, ct);

        // custoUnitário é por UNIDADE inteira (Money); quantidade é em milésimos — mesma convenção
        // de escala de VendaItensMovimentadosHandler/CalculadoraDeCustoMedio.
        var custoTotalCentavos = (long)Math.Round(custoUnitarioNoInstanteDaBaixa.Centavos * quantidade.Milesimos / 1000m, MidpointRounding.ToEven);
        await bus.PublishAsync(new CustoBaixadoPorOs(evento.OrdemServicoId, evento.TenantId, evento.LinhaId, custoTotalCentavos, evento.OcorridoEm), ct);
    }
}
