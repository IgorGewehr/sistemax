using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.Modules.Estoque.Domain.Saldos;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// <c>venda.estornada</c> não carrega itens — o Estoque estorna pelo PRÓPRIO razão (busca os
/// movimentos de Saida com Origem=venda e espelha cada um em Entrada). Isso garante que o estorno
/// devolve exatamente o que saiu, mesmo que a ficha técnica do produto já tenha mudado.
/// </summary>
public class VendaEstornadaHandlerTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task HandleAsync_VendaComDoisItens_EstornaOsDoisMovimentosDeSaida()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produtoA = Produto.Criar(TenantId, "Produto A", UnidadeDeMedida.UN).Valor;
        var produtoB = Produto.Criar(TenantId, "Produto B", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produtoA);
        await produtos.SalvarAsync(produtoB);

        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, new FakeIntegrationEventBus());
        var evento = new VendaItensMovimentados("venda-1", TenantId,
        [
            new ItemMovimentado(produtoA.Id, produtoA.Nome, Quantidade.DeInteiro(3).Milesimos, 1000, ItemId: "item-a"),
            new ItemMovimentado(produtoB.Id, produtoB.Nome, Quantidade.DeInteiro(1).Milesimos, 5000, ItemId: "item-b")
        ], DateTimeOffset.UtcNow);
        await vendaHandler.HandleAsync(evento);

        var estornoHandler = new VendaEstornadaHandler(movimentos, saldos);
        await estornoHandler.HandleAsync(new VendaEstornada("venda-1", TenantId, 8000, DateTimeOffset.UtcNow.AddMinutes(5)));

        var saldoA = await saldos.ObterAsync(TenantId, produtoA.Id, "principal");
        var saldoB = await saldos.ObterAsync(TenantId, produtoB.Id, "principal");

        Assert.Equal(Quantidade.Zero, saldoA!.Fisico); // -3 (venda) +3 (estorno) = 0
        Assert.Equal(Quantidade.Zero, saldoB!.Fisico);
    }

    [Fact]
    public async Task HandleAsync_ChamadoDuasVezes_NaoDuplicaOEstorno()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produto = Produto.Criar(TenantId, "Produto A", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, new FakeIntegrationEventBus());
        await vendaHandler.HandleAsync(new VendaItensMovimentados("venda-2", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(2).Milesimos, 1000, ItemId: "item-a")],
            DateTimeOffset.UtcNow));

        var estornoHandler = new VendaEstornadaHandler(movimentos, saldos);
        var estornoEvento = new VendaEstornada("venda-2", TenantId, 2000, DateTimeOffset.UtcNow.AddMinutes(5));

        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, produto.Id, "principal");
        Assert.Equal(2, razao.Count); // 1 saída original + 1 único estorno
    }
}
