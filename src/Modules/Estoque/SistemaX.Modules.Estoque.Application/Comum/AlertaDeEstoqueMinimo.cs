using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Saldos;

namespace SistemaX.Modules.Estoque.Application.Comum;

/// <summary>
/// Publica <c>EstoqueAbaixoDoMinimo</c> SÓ NA TRANSIÇÃO (estava acima do mínimo, cruzou para
/// baixo agora) — anti-ruído: uma sequência de vendas com o saldo já baixo não reemite o alerta a
/// cada uma. Compartilhado entre os handlers que causam <c>Saida</c> (venda, consumo de OS).
/// </summary>
public static class AlertaDeEstoqueMinimo
{
    public static Task AvaliarEPublicarAsync(
        IIntegrationEventBus bus, Produto produto, Quantidade disponivelAntes, SaldoDeItem saldoDepois,
        string movimentoId, string tenantId, DateTimeOffset ocorridoEm, CancellationToken ct = default)
    {
        if (produto.EstoqueMinimo.EhZero)
            return Task.CompletedTask; // sem mínimo configurado — sem alerta possível

        var cruzouParaBaixo = disponivelAntes > produto.EstoqueMinimo && saldoDepois.Disponivel <= produto.EstoqueMinimo;
        if (!cruzouParaBaixo)
            return Task.CompletedTask;

        return bus.PublishAsync(new EstoqueAbaixoDoMinimo(
            produto.Id, tenantId, produto.Nome, saldoDepois.Disponivel.Milesimos, produto.EstoqueMinimo.Milesimos,
            movimentoId, ocorridoEm), ct);
    }
}
