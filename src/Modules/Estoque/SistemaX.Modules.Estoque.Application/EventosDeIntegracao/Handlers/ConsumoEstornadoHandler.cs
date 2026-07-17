using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;

namespace SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// <c>os.estorno</c> → baixa já feita volta ao físico (cancelamento em execução). MVP: estorno
/// integral pelo custo médio VIGENTE (não pelo custo congelado da baixa original) — "peça
/// inutilizada, não volta pro físico" é gesto manual de <c>RegistrarPerdaUseCase</c> depois.
///
/// P0-5 (docs/financeiro/revisao-domain-fit-cnpj.md): companion simétrico de
/// <see cref="PecaConsumidaHandler"/> — publica <see cref="CustoBaixadoPorOs"/> com
/// <c>Estornado: true</c> e <see cref="CustoBaixadoPorOs.CustoTotalCentavos"/> NEGATIVO (mesmo
/// custo médio vigente usado no estorno físico acima), pra que <c>fato_custo_diario</c> reverta
/// exatamente o CMV que havia entrado na baixa original — nunca edita o fato original, só soma um
/// delta negativo (mesmo racional de <c>VendaEstornada</c>).
/// </summary>
public sealed class ConsumoEstornadoHandler(IProdutoRepository produtos, IMovimentoRepository movimentos, ISaldoRepository saldos, IIntegrationEventBus bus)
    : IIntegrationEventHandler<ConsumoEstornado>
{
    public async Task HandleAsync(ConsumoEstornado evento, CancellationToken ct = default)
    {
        var chave = $"os.estorno:{evento.OrdemServicoId}:{evento.LinhaId}";
        if (await movimentos.ExisteComChaveAsync(chave, ct)) return;

        var produto = await produtos.ObterPorIdAsync(evento.ProdutoId, ct);
        if (produto is null || !produto.ControlaEstoque) return;

        var saldo = await saldos.ObterOuCriarAsync(evento.TenantId, evento.ProdutoId, EstoqueConstantes.DepositoPadrao, ct);
        var custoUnitarioVigente = saldo.CustoMedio;

        var movimentoResultado = MovimentoDeEstoque.Registrar(
            evento.TenantId, EstoqueConstantes.DepositoPadrao, evento.ProdutoId, TipoMovimento.Entrada,
            new Quantidade(evento.QuantidadeMilesimos), custoUnitarioVigente, new SourceRef("os", evento.OrdemServicoId), chave,
            $"Estorno de consumo — OS {evento.OrdemServicoId}", EstoqueConstantes.OperadorSistema,
            EstoqueConstantes.OperadorSistemaNome, evento.OcorridoEm);

        if (movimentoResultado.Falha)
            throw new InvalidOperationException($"Falha ao estornar consumo da OS {evento.OrdemServicoId}: {movimentoResultado.Erro.Mensagem}");

        saldo.AplicarMovimento(movimentoResultado.Valor);
        await movimentos.SalvarAsync(movimentoResultado.Valor, ct);
        await saldos.SalvarAsync(saldo, ct);

        var custoTotalCentavos = (long)Math.Round(
            custoUnitarioVigente.Centavos * evento.QuantidadeMilesimos / 1000m, MidpointRounding.ToEven);
        await bus.PublishAsync(new CustoBaixadoPorOs(
            evento.OrdemServicoId, evento.TenantId, evento.LinhaId, -custoTotalCentavos, evento.OcorridoEm, Estornado: true), ct);
    }
}
