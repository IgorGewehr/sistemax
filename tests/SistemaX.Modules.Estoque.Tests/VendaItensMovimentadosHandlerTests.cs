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

public class VendaItensMovimentadosHandlerTests
{
    private const string TenantId = "tenant-1";

    private static (InMemoryProdutoRepository Produtos, InMemoryMovimentoRepository Movimentos, InMemorySaldoRepository Saldos, FakeIntegrationEventBus Bus, VendaItensMovimentadosHandler Handler) MontarCenario()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();
        var handler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);
        return (produtos, movimentos, saldos, bus, handler);
    }

    [Fact]
    public async Task HandleAsync_ProdutoSimples_GeraSaidaEAtualizaSaldo()
    {
        var (produtos, movimentos, saldos, _, handler) = MontarCenario();

        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN, precoVenda: Money.DeReais(20)).Valor;
        await produtos.SalvarAsync(produto);
        await saldos.SalvarAsync(SaldoDeItemComFisico(produto.Id, 50, Money.DeReais(8)));

        var evento = new VendaItensMovimentados("venda-1", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(2).Milesimos, 2000, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(48), saldo!.Fisico);

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, produto.Id, "principal");
        Assert.Single(razao);
        Assert.Equal(Money.DeReais(8), razao[0].CustoUnitario); // baixa congela o custo médio vigente
    }

    [Fact]
    public async Task HandleAsync_ProdutoComFichaTecnica_ExpandeNosInsumos()
    {
        var (produtos, movimentos, saldos, _, handler) = MontarCenario();

        var farinha = Produto.Criar(TenantId, "Farinha (KG)", UnidadeDeMedida.KG).Valor;
        var queijo = Produto.Criar(TenantId, "Queijo (KG)", UnidadeDeMedida.KG).Valor;
        await produtos.SalvarAsync(farinha);
        await produtos.SalvarAsync(queijo);
        await saldos.SalvarAsync(SaldoDeItemComFisico(farinha.Id, 10, Money.DeReais(4)));
        await saldos.SalvarAsync(SaldoDeItemComFisico(queijo.Id, 10, Money.DeReais(30)));

        var ficha = new[]
        {
            new ComponenteDeFicha(farinha.Id, Quantidade.DeDecimal(0.3m)),
            new ComponenteDeFicha(queijo.Id, Quantidade.DeDecimal(0.2m))
        };
        var pizza = Produto.Criar(TenantId, "Pizza Calabresa", UnidadeDeMedida.UN, fichaTecnica: ficha).Valor;
        await produtos.SalvarAsync(pizza);

        var evento = new VendaItensMovimentados("venda-2", TenantId,
            [new ItemMovimentado(pizza.Id, pizza.Nome, Quantidade.DeInteiro(2).Milesimos, 4000, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);

        // 2 pizzas × 0,3 KG farinha = 0,6 KG; 2 pizzas × 0,2 KG queijo = 0,4 KG
        var saldoFarinha = await saldos.ObterAsync(TenantId, farinha.Id, "principal");
        var saldoQueijo = await saldos.ObterAsync(TenantId, queijo.Id, "principal");
        Assert.Equal(Quantidade.DeDecimal(9.4m), saldoFarinha!.Fisico);
        Assert.Equal(Quantidade.DeDecimal(9.6m), saldoQueijo!.Fisico);

        var razaoPizza = await movimentos.ListarPorProdutoAsync(TenantId, pizza.Id, "principal");
        Assert.Empty(razaoPizza); // produto composto nunca tem movimento próprio
    }

    [Fact]
    public async Task HandleAsync_ProdutoSemControlarEstoque_NaoGeraMovimento()
    {
        var (produtos, movimentos, saldos, _, handler) = MontarCenario();
        var taxa = Produto.Criar(TenantId, "Taxa de diagnóstico", UnidadeDeMedida.UN, controlaEstoque: false).Valor;
        await produtos.SalvarAsync(taxa);

        var evento = new VendaItensMovimentados("venda-3", TenantId,
            [new ItemMovimentado(taxa.Id, taxa.Nome, Quantidade.DeInteiro(1).Milesimos, 5000, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, taxa.Id, "principal");
        Assert.Empty(razao);
    }

    [Fact]
    public async Task HandleAsync_ChamadoDuasVezes_ReplayENaoDuplicaMovimento()
    {
        var (produtos, movimentos, saldos, _, handler) = MontarCenario();
        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);
        await saldos.SalvarAsync(SaldoDeItemComFisico(produto.Id, 50, Money.DeReais(8)));

        var evento = new VendaItensMovimentados("venda-4", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(2).Milesimos, 2000, ItemId: "item-1")],
            DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento); // replay do mesmo evento

        var razao = await movimentos.ListarPorProdutoAsync(TenantId, produto.Id, "principal");
        Assert.Single(razao);

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(48), saldo!.Fisico);
    }

    [Fact]
    public async Task HandleAsync_DisponivelCruzaOMinimo_PublicaAlertaSoNaTransicao()
    {
        var (produtos, movimentos, saldos, bus, handler) = MontarCenario();
        var produto = Produto.Criar(TenantId, "Tela iPhone 13", UnidadeDeMedida.UN, estoqueMinimo: Quantidade.DeInteiro(4)).Valor;
        await produtos.SalvarAsync(produto);
        await saldos.SalvarAsync(SaldoDeItemComFisico(produto.Id, 5, Money.DeReais(180)));

        // primeira venda: 5 -> 4 (cruza a igualdade do mínimo — dispara)
        await handler.HandleAsync(new VendaItensMovimentados("venda-5", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(1).Milesimos, 25000, ItemId: "item-1")],
            DateTimeOffset.UtcNow));

        // F0 do plano de inteligência do Financeiro: toda baixa também publica CustoBaixadoPorVenda
        // (CMV) — o teste filtra por tipo em vez de assumir que só existe um evento no bus.
        Assert.Single(bus.Publicados.OfType<EstoqueAbaixoDoMinimo>());
        Assert.Single(bus.Publicados.OfType<CustoBaixadoPorVenda>());

        // segunda venda: 4 -> 3 (já estava abaixo — não deve re-alertar)
        await handler.HandleAsync(new VendaItensMovimentados("venda-6", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(1).Milesimos, 25000, ItemId: "item-1")],
            DateTimeOffset.UtcNow));

        Assert.Single(bus.Publicados.OfType<EstoqueAbaixoDoMinimo>()); // nenhum alerta novo
        Assert.Equal(2, bus.Publicados.OfType<CustoBaixadoPorVenda>().Count()); // CMV da venda-6 também publicado
    }

    private static SaldoDeItem SaldoDeItemComFisico(string produtoId, int unidades, Money custoMedio)
    {
        var saldo = SaldoDeItem.Vazio(TenantId, produtoId, "principal");
        var entrada = MovimentoDeEstoque.Registrar(
            TenantId, "principal", produtoId, TipoMovimento.Entrada, Quantidade.DeInteiro(unidades), custoMedio,
            new SourceRef("manual", "seed"), $"seed:{produtoId}", "carga inicial de teste", "op", "Operador",
            DateTimeOffset.UtcNow.AddDays(-30)).Valor;
        saldo.AplicarMovimento(entrada);
        return saldo;
    }
}
