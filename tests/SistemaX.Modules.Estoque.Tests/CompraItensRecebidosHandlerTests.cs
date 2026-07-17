using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

public class CompraItensRecebidosHandlerTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task HandleAsync_PrimeiraCompra_CriaEntradaEAdotaCustoDaNota()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var handler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);

        var produto = Produto.Criar(TenantId, "Bateria iPhone 11", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var evento = new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, 10_000, 9640, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(10, saldo!.Fisico.EmDecimal);
        Assert.Equal(Money.DeReais(96.40m), saldo.CustoMedio);
    }

    [Fact]
    public async Task HandleAsync_SegundaCompraComCustoDiferente_RecalculaCustoMedioPonderado()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var handler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);

        var produto = Produto.Criar(TenantId, "Bateria iPhone 11", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        await handler.HandleAsync(new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(10).Milesimos, 10000, ItemId: "item-1")],
            DateTimeOffset.UtcNow));

        await handler.HandleAsync(new CompraItensRecebidos("compra-2", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(10).Milesimos, 20000, ItemId: "item-1")],
            DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(20, saldo!.Fisico.EmDecimal);
        Assert.Equal(Money.DeReais(150), saldo.CustoMedio); // (10*100 + 10*200) / 20 = 150
    }

    [Fact]
    public async Task HandleAsync_ChamadoDuasVezesComMesmoEvento_NaoDuplicaEntrada()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var handler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);

        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var evento = new CompraItensRecebidos("compra-3", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(50).Milesimos, 890, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento);

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, produto.Id, "principal");
        Assert.Single(razao);
    }

    [Fact]
    public async Task HandleAsync_ProdutoQueNaoControlaEstoque_NaoGeraEntrada()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var handler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);

        var frete = Produto.Criar(TenantId, "Frete", UnidadeDeMedida.UN, controlaEstoque: false).Valor;
        await produtos.SalvarAsync(frete);

        await handler.HandleAsync(new CompraItensRecebidos("compra-4", TenantId, "fornecedor-1",
            [new ItemMovimentado(frete.Id, frete.Nome, Quantidade.DeInteiro(1).Milesimos, 1500, ItemId: "item-1")],
            DateTimeOffset.UtcNow));

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, frete.Id, "principal");
        Assert.Empty(razao);
    }
}
