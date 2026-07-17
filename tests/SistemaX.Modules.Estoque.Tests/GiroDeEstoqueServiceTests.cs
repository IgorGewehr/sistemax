using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Application.ReadModels;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

public class GiroDeEstoqueServiceTests
{
    private const string TenantId = "tenant-1";
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero); // janela de 30 dias

    [Fact]
    public async Task CalcularAsync_VendeuMetadeDoEstoqueNoPeriodo_CalculaGiroECoberturaCoerentes()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(10).Milesimos, 1000, ItemId: "item-1")],
            Inicio.AddDays(-1)));

        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);
        await vendaHandler.HandleAsync(new VendaItensMovimentados("venda-1", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(5).Milesimos, 2000, ItemId: "item-1")],
            Inicio.AddDays(1)));

        var servico = new GiroDeEstoqueService(movimentos, saldos, produtos);
        var linhas = await servico.CalcularAsync(TenantId, Inicio, Fim);

        var linha = Assert.Single(linhas);
        Assert.Equal(produto.Id, linha.ProdutoId);
        Assert.Equal(Money.DeReais(50), linha.CmvNoPeriodo); // 5 un × R$10
        Assert.Equal(Money.DeReais(50), linha.ValorImobilizadoAtual); // 5 un restantes × R$10
        Assert.Equal(30, linha.CoberturaDias!.Value); // consumo 5 em 30 dias = 1/6 por dia; disponível 5 ÷ 1/6 = 30
        Assert.True(linha.GiroAnualizado > 0);
    }

    [Fact]
    public async Task CalcularAsync_ProdutoSemSaidaNoPeriodo_NaoAparaceNoRanking()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produto = Produto.Criar(TenantId, "Produto parado", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-2", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(10).Milesimos, 1000, ItemId: "item-1")],
            Inicio.AddDays(-1)));

        var servico = new GiroDeEstoqueService(movimentos, saldos, produtos);
        var linhas = await servico.CalcularAsync(TenantId, Inicio, Fim);

        Assert.Empty(linhas); // sem Saida no período — não tem giro a reportar (ele "gira" zero, não "não existe")
    }
}
