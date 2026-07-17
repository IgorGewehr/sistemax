using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Application.ReadModels;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// Ruptura é reconstruída por REPLAY COMPLETO do razão (nunca só os movimentos dentro da janela) —
/// só a CONTAGEM de dias em ruptura é recortada para [inicio, fim].
/// </summary>
public class RupturaServiceTests
{
    private const string TenantId = "tenant-1";
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2026, 1, 11, 0, 0, 0, TimeSpan.Zero); // janela de 10 dias

    [Fact]
    public async Task AnalisarAsync_ProdutoZeraDentroDaJanela_ContaDiasEEstimaVendaPerdida()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var produto = Produto.Criar(TenantId, "Tela iPhone 13", UnidadeDeMedida.UN, precoVenda: Money.DeReais(50)).Valor;
        await produtos.SalvarAsync(produto);

        // entrada ANTES da janela (fora do período — mas entra no replay pra saber o ponto de partida)
        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(5).Milesimos, 2000, ItemId: "item-1")],
            Inicio.AddDays(-5)));

        // saída DENTRO da janela zera o saldo em Inicio+2
        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);
        await vendaHandler.HandleAsync(new VendaItensMovimentados("venda-1", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(5).Milesimos, 4000, ItemId: "item-1")],
            Inicio.AddDays(2)));

        var servico = new RupturaService(movimentos, produtos);
        var linhas = await servico.AnalisarAsync(TenantId, Inicio, Fim);

        var linha = Assert.Single(linhas);
        Assert.Equal(produto.Id, linha.ProdutoId);
        Assert.Equal(8, linha.DiasEmRuptura); // de Inicio+2 até Fim (janela de 10 dias) = 8 dias zerado
        Assert.Equal(Money.DeReais(200), linha.VendaPerdidaEstimada); // 0,5 un/dia × 8 dias × R$50
    }

    [Fact]
    public async Task AnalisarAsync_ProdutoSempreComSaldoPositivo_NaoAparaceNaAnalise()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN, precoVenda: Money.DeReais(20)).Valor;
        await produtos.SalvarAsync(produto);

        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-2", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(100).Milesimos, 500, ItemId: "item-1")],
            Inicio.AddDays(-5)));

        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);
        await vendaHandler.HandleAsync(new VendaItensMovimentados("venda-2", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(5).Milesimos, 1000, ItemId: "item-1")],
            Inicio.AddDays(2)));

        var servico = new RupturaService(movimentos, produtos);
        var linhas = await servico.AnalisarAsync(TenantId, Inicio, Fim);

        Assert.Empty(linhas);
    }
}
