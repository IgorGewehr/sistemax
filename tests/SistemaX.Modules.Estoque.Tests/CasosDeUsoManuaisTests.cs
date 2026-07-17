using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.CasosDeUso;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

public class CasosDeUsoManuaisTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task CriarProduto_DadosValidos_PersisteNoCatalogo()
    {
        var produtos = new InMemoryProdutoRepository();
        var useCase = new CriarProdutoUseCase(produtos);

        var resultado = await useCase.ExecutarAsync(
            TenantId, "Refrigerante Lata 350ml", UnidadeDeMedida.UN,
            sku: "REF-350", precoVenda: Money.DeReais(5.5m), categoria: "Bebidas");

        Assert.True(resultado.Sucesso);
        var persistido = await produtos.ObterPorIdAsync(resultado.Valor.Id);
        Assert.NotNull(persistido);
        Assert.Equal("REF-350", persistido!.Sku);
        Assert.Equal(Money.DeReais(5.5m), persistido.PrecoVenda);
    }

    [Fact]
    public async Task CriarProduto_SkuJaCadastradoNoTenant_Falha()
    {
        var produtos = new InMemoryProdutoRepository();
        var useCase = new CriarProdutoUseCase(produtos);

        var primeiro = await useCase.ExecutarAsync(TenantId, "Produto A", UnidadeDeMedida.UN, sku: "DUP-1");
        Assert.True(primeiro.Sucesso);

        var segundo = await useCase.ExecutarAsync(TenantId, "Produto B", UnidadeDeMedida.UN, sku: "DUP-1");

        Assert.True(segundo.Falha);
        Assert.Equal("estoque.produto.sku_duplicado", segundo.Erro.Codigo);
    }

    [Fact]
    public async Task RegistrarEntradaManual_ProdutoQueControlaEstoque_GeraEntrada()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var produto = Produto.Criar(TenantId, "Produto implantação", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var useCase = new RegistrarEntradaManualUseCase(produtos, movimentos, saldos);
        var resultado = await useCase.ExecutarAsync(
            TenantId, produto.Id, Quantidade.DeInteiro(20), Money.DeReais(15), "Carga inicial de implantação",
            "op-1", "Igor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(20), saldo!.Fisico);
        Assert.Equal(Money.DeReais(15), saldo.CustoMedio);
    }

    [Fact]
    public async Task RegistrarEntradaManual_ProdutoQueNaoControlaEstoque_Falha()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var taxa = Produto.Criar(TenantId, "Taxa", UnidadeDeMedida.UN, controlaEstoque: false).Valor;
        await produtos.SalvarAsync(taxa);

        var useCase = new RegistrarEntradaManualUseCase(produtos, movimentos, saldos);
        var resultado = await useCase.ExecutarAsync(TenantId, taxa.Id, Quantidade.DeInteiro(1), Money.Zero, "n/a", "op-1", "Igor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.produto.nao_controla_estoque", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task RegistrarPerda_ComMotivo_GeraSaidaEPublicaPerdaRegistrada()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var produto = Produto.Criar(TenantId, "Removedor OCA 250ml", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var entradaUseCase = new RegistrarEntradaManualUseCase(produtos, movimentos, saldos);
        await entradaUseCase.ExecutarAsync(TenantId, produto.Id, Quantidade.DeInteiro(10), Money.DeReais(18.10m), "carga", "op-1", "Igor", DateTimeOffset.UtcNow);

        var perdaUseCase = new RegistrarPerdaUseCase(produtos, movimentos, saldos, bus);
        var resultado = await perdaUseCase.ExecutarAsync(TenantId, produto.Id, Quantidade.DeInteiro(3), "validade vencida", "op-1", "Igor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(7), saldo!.Fisico);

        var evento = Assert.IsType<PerdaRegistrada>(Assert.Single(bus.Publicados));
        Assert.Equal("validade vencida", evento.Motivo);
        Assert.Equal(Money.DeReais(54.30m), new Money(evento.CustoTotalCentavos));
    }

    [Fact]
    public async Task RegistrarPerda_SemMotivo_Falha()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();
        var produto = Produto.Criar(TenantId, "Produto X", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var perdaUseCase = new RegistrarPerdaUseCase(produtos, movimentos, saldos, bus);
        var resultado = await perdaUseCase.ExecutarAsync(TenantId, produto.Id, Quantidade.DeInteiro(1), "", "op-1", "Igor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Empty(bus.Publicados);
    }

    [Fact]
    public async Task RecalcularSaldo_ReproduzExatamenteOSaldoJaAplicado()
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();
        var bus = new FakeIntegrationEventBus();

        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-1", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(20).Milesimos, 1000, ItemId: "item-1")],
            DateTimeOffset.UtcNow.AddDays(-2)));

        var vendaHandler = new VendaItensMovimentadosHandler(produtos, movimentos, saldos, bus);
        await vendaHandler.HandleAsync(new VendaItensMovimentados("venda-1", TenantId,
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(7).Milesimos, 2000, ItemId: "item-1")],
            DateTimeOffset.UtcNow.AddDays(-1)));

        var saldoCache = await saldos.ObterAsync(TenantId, produto.Id, "principal");

        var recalcularUseCase = new RecalcularSaldoUseCase(movimentos, saldos);
        var saldoRecalculado = await recalcularUseCase.ExecutarAsync(TenantId, produto.Id, "principal");

        Assert.Equal(saldoCache!.Fisico, saldoRecalculado.Fisico);
        Assert.Equal(saldoCache.CustoMedio, saldoRecalculado.CustoMedio);
    }
}
