using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// <c>compra.estornada</c> — fecha o GAP DOCUMENTADO em
/// <c>SistemaX.Modules.Abstractions.IntegrationEvents.CompraEstornada</c>: reverte item a item o
/// que <see cref="CompraItensRecebidosHandler"/> creditou, usando os itens do PRÓPRIO evento
/// (que replica os de <c>CompraItensRecebidos</c>).
/// </summary>
public class CompraEstornadaHandlerTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task HandleAsync_CompraComDoisItens_EstornaOsDoisComSaida()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produtoA = Produto.Criar(TenantId, "Produto A", UnidadeDeMedida.UN).Valor;
        var produtoB = Produto.Criar(TenantId, "Produto B", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produtoA);
        await produtos.SalvarAsync(produtoB);

        var itens = new[]
        {
            new ItemMovimentado(produtoA.Id, produtoA.Nome, Quantidade.DeInteiro(3).Milesimos, 1000, ItemId: "item-a"),
            new ItemMovimentado(produtoB.Id, produtoB.Nome, Quantidade.DeInteiro(1).Milesimos, 5000, ItemId: "item-b")
        };

        var entradaHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await entradaHandler.HandleAsync(new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1", itens, DateTimeOffset.UtcNow));

        var estornoHandler = new CompraEstornadaHandler(produtos, movimentos, saldos);
        await estornoHandler.HandleAsync(new CompraEstornada("compra-1", TenantId, "fornecedor-1", itens, 8000, DateTimeOffset.UtcNow.AddMinutes(5)));

        var saldoA = await saldos.ObterAsync(TenantId, produtoA.Id, "principal");
        var saldoB = await saldos.ObterAsync(TenantId, produtoB.Id, "principal");

        Assert.Equal(Quantidade.Zero, saldoA!.Fisico); // +3 (compra) -3 (estorno) = 0
        Assert.Equal(Quantidade.Zero, saldoB!.Fisico);
    }

    [Fact]
    public async Task HandleAsync_ChamadoDuasVezesComMesmoEvento_NaoDuplicaOEstorno()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produto = Produto.Criar(TenantId, "Produto A", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var itens = new[] { new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(2).Milesimos, 1000, ItemId: "item-a") };

        var entradaHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await entradaHandler.HandleAsync(new CompraItensRecebidos("compra-2", TenantId, "fornecedor-1", itens, DateTimeOffset.UtcNow));

        var estornoHandler = new CompraEstornadaHandler(produtos, movimentos, saldos);
        var estornoEvento = new CompraEstornada("compra-2", TenantId, "fornecedor-1", itens, 2000, DateTimeOffset.UtcNow.AddMinutes(5));

        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, produto.Id, "principal");
        Assert.Equal(2, razao.Count); // 1 entrada original + 1 único estorno
    }

    [Fact]
    public async Task HandleAsync_ProdutoQueNaoControlaEstoque_NaoGeraMovimento()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var frete = Produto.Criar(TenantId, "Frete", UnidadeDeMedida.UN, controlaEstoque: false).Valor;
        await produtos.SalvarAsync(frete);

        var itens = new[] { new ItemMovimentado(frete.Id, frete.Nome, Quantidade.DeInteiro(1).Milesimos, 1500, ItemId: "item-1") };

        var estornoHandler = new CompraEstornadaHandler(produtos, movimentos, saldos);
        await estornoHandler.HandleAsync(new CompraEstornada("compra-3", TenantId, "fornecedor-1", itens, 1500, DateTimeOffset.UtcNow));

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, frete.Id, "principal");
        Assert.Empty(razao);
    }
}
