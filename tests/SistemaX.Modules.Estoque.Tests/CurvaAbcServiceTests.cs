using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Application.ReadModels;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>Corte 80/15/5 clássico da Curva ABC — plano §6/R2.</summary>
public class CurvaAbcServiceTests
{
    private const string TenantId = "tenant-1";
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClassificarAsync_TresProdutos_ClassificaPeloValorDeCustoBaixado()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);

        // A: R$700 (70%) · B: R$200 (cumulativo 90%) · C: R$100 (cumulativo 100%)
        var produtoA = await CriarEVenderAsync(produtos, compraHandler, vendaHandler, "Produto A", 70000);
        var produtoB = await CriarEVenderAsync(produtos, compraHandler, vendaHandler, "Produto B", 20000);
        var produtoC = await CriarEVenderAsync(produtos, compraHandler, vendaHandler, "Produto C", 10000);

        var servico = new CurvaAbcService(movimentos, produtos);
        var curva = await servico.ClassificarAsync(TenantId, Inicio, Fim);

        Assert.Equal(3, curva.Count);
        Assert.Equal('A', curva.Single(l => l.ProdutoId == produtoA).Classe);
        Assert.Equal(70.0m, curva.Single(l => l.ProdutoId == produtoA).PercentualAcumulado);
        Assert.Equal('B', curva.Single(l => l.ProdutoId == produtoB).Classe);
        Assert.Equal(90.0m, curva.Single(l => l.ProdutoId == produtoB).PercentualAcumulado);
        Assert.Equal('C', curva.Single(l => l.ProdutoId == produtoC).Classe);
        Assert.Equal(100.0m, curva.Single(l => l.ProdutoId == produtoC).PercentualAcumulado);
    }

    private static async Task<string> CriarEVenderAsync(
        InMemoryProdutoRepository produtos, CompraItensRecebidosHandler compraHandler, VendaItensMovimentadosHandler vendaHandler,
        string nome, long custoCentavos)
    {
        var produto = Produto.Criar(TenantId, nome, UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        await compraHandler.HandleAsync(new CompraItensRecebidos($"compra-{nome}", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, nome, Quantidade.DeInteiro(1).Milesimos, custoCentavos, ItemId: "item-1")],
            Inicio.AddDays(1)));

        await vendaHandler.HandleAsync(new VendaItensMovimentados($"venda-{nome}", TenantId,
            [new ItemMovimentado(produto.Id, nome, Quantidade.DeInteiro(1).Milesimos, custoCentavos * 2, ItemId: "item-1")],
            Inicio.AddDays(2)));

        return produto.Id;
    }
}
