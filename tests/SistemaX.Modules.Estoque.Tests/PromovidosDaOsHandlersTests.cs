using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// Os 4 eventos promovidos da OS (Assistência) — reserva NUNCA bloqueia (só avisa via
/// <c>ReservaDescoberta</c> quando descoberta), consumo libera a reserva da linha e baixa o
/// físico na mesma operação lógica.
/// </summary>
public class PromovidosDaOsHandlersTests
{
    private const string TenantId = "tenant-1";

    private static async Task<(InMemoryProdutoRepository Produtos, InMemoryMovimentoRepository Movimentos, InMemorySaldoRepository Saldos, Produto Produto)> MontarCenarioComProdutoAsync(int fisicoInicial, decimal custoMedio)
    {
        var produtos = new InMemoryProdutoRepository();
        var movimentos = new InMemoryMovimentoRepository();
        var saldos = new InMemorySaldoRepository();

        var produto = Produto.Criar(TenantId, "Tela iPhone 13", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        // carga inicial via CompraItensRecebidos (handler real, não seed direto) — mantém o razão coerente.
        var compraHandler = new CompraItensRecebidosHandler(produtos, movimentos, saldos);
        await compraHandler.HandleAsync(new CompraItensRecebidos("compra-seed", TenantId, "fornecedor-1",
            [new ItemMovimentado(produto.Id, produto.Nome, Quantidade.DeInteiro(fisicoInicial).Milesimos, (long)(custoMedio * 100), ItemId: "item-seed")],
            DateTimeOffset.UtcNow.AddDays(-10)));

        return (produtos, movimentos, saldos, produto);
    }

    [Fact]
    public async Task PecaReservada_ComSaldoSuficiente_ReservaSemAvisoDeDescoberta()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var handler = new PecaReservadaHandler(produtos, movimentos, saldos, bus);

        await handler.HandleAsync(new PecaReservada("os-1", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(2), saldo!.Reservado);
        Assert.Equal(Quantidade.DeInteiro(3), saldo.Disponivel);
        Assert.Empty(bus.Publicados);
    }

    [Fact]
    public async Task PecaReservada_SemSaldoSuficiente_ReservaMesmoAssimEPublicaReservaDescoberta()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(1, 180m);
        var bus = new FakeIntegrationEventBus();
        var handler = new PecaReservadaHandler(produtos, movimentos, saldos, bus);

        await handler.HandleAsync(new PecaReservada("os-2", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(3).Milesimos, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(3), saldo!.Reservado);
        Assert.Equal(new Quantidade(-2000), saldo.Disponivel); // reserva descoberta — nunca bloqueia

        var descoberta = Assert.Single(bus.Publicados);
        var reservaDescoberta = Assert.IsType<ReservaDescoberta>(descoberta);
        Assert.Equal(2000, reservaDescoberta.FaltamMilesimos);
    }

    [Fact]
    public async Task PecaConsumida_ComReservaPrevia_LiberaAReservaEBaixaOFisicoNaMesmaOperacao()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var reservaHandler = new PecaReservadaHandler(produtos, movimentos, saldos, bus);
        await reservaHandler.HandleAsync(new PecaReservada("os-3", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);
        await consumoHandler.HandleAsync(new PecaConsumida("os-3", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.Zero, saldo!.Reservado); // reserva consumida integralmente
        Assert.Equal(Quantidade.DeInteiro(3), saldo.Fisico); // 5 - 2 baixados
        Assert.Equal(Quantidade.DeInteiro(3), saldo.Disponivel);
    }

    [Fact]
    public async Task PecaConsumida_SemReservaPrevia_ApenasBaixaOFisico()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);

        await consumoHandler.HandleAsync(new PecaConsumida("os-4", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(1).Milesimos, 25000, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.Zero, saldo!.Reservado);
        Assert.Equal(Quantidade.DeInteiro(4), saldo.Fisico);
    }

    [Fact]
    public async Task PecaConsumida_PublicaCustoBaixadoPorOsComOCustoMedioVigenteXQuantidade()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m); // custo médio: R$180,00/un
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);

        await consumoHandler.HandleAsync(new PecaConsumida("os-custo-1", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow));

        var publicado = Assert.Single(bus.Publicados);
        var custo = Assert.IsType<CustoBaixadoPorOs>(publicado);
        Assert.Equal("os-custo-1", custo.OrdemServicoId);
        Assert.Equal("linha-1", custo.LinhaId);
        Assert.Equal(36_000, custo.CustoTotalCentavos); // 2 × R$180,00 = R$360,00, positivo (baixa)
        Assert.False(custo.Estornado);
    }

    [Fact]
    public async Task PecaConsumida_ChamadoDuasVezes_NaoPublicaCustoDuplicado()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);
        var evento = new PecaConsumida("os-custo-2", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow);

        await consumoHandler.HandleAsync(evento);
        await consumoHandler.HandleAsync(evento); // replay

        Assert.Single(bus.Publicados);
    }

    [Fact]
    public async Task ReservaLiberada_DevolveAoDisponivelSemTocarOFisico()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var reservaHandler = new PecaReservadaHandler(produtos, movimentos, saldos, bus);
        await reservaHandler.HandleAsync(new PecaReservada("os-5", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var liberacaoHandler = new ReservaLiberadaHandler(produtos, movimentos, saldos);
        await liberacaoHandler.HandleAsync(new ReservaLiberada("os-5", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.Zero, saldo!.Reservado);
        Assert.Equal(Quantidade.DeInteiro(5), saldo.Fisico); // físico intacto
        Assert.Equal(Quantidade.DeInteiro(5), saldo.Disponivel);
    }

    [Fact]
    public async Task ConsumoEstornado_DevolveAoFisicoPeloCustoMedioVigente()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);
        await consumoHandler.HandleAsync(new PecaConsumida("os-6", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow));

        var estornoHandler = new ConsumoEstornadoHandler(produtos, movimentos, saldos, bus);
        await estornoHandler.HandleAsync(new ConsumoEstornado("os-6", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(5), saldo!.Fisico); // 5 - 2 (consumo) + 2 (estorno) = 5
    }

    [Fact]
    public async Task ConsumoEstornado_PublicaCustoBaixadoPorOsNegativoComChavePropria()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);
        await consumoHandler.HandleAsync(new PecaConsumida("os-7", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow));

        var estornoHandler = new ConsumoEstornadoHandler(produtos, movimentos, saldos, bus);
        await estornoHandler.HandleAsync(new ConsumoEstornado("os-7", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow));

        var custos = bus.Publicados.OfType<CustoBaixadoPorOs>().ToList();
        Assert.Equal(2, custos.Count);
        Assert.Equal(36_000, custos[0].CustoTotalCentavos); // baixa original: positivo
        Assert.False(custos[0].Estornado);
        Assert.Equal(-36_000, custos[1].CustoTotalCentavos); // estorno: negativo, mesma magnitude
        Assert.True(custos[1].Estornado);
        Assert.NotEqual(custos[0].ChaveIdempotencia, custos[1].ChaveIdempotencia);
    }

    [Fact]
    public async Task ConsumoEstornado_ChamadoDuasVezes_NaoPublicaEstornoDeCustoDuplicado()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var consumoHandler = new PecaConsumidaHandler(produtos, movimentos, saldos, bus);
        await consumoHandler.HandleAsync(new PecaConsumida("os-8", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, 25000, DateTimeOffset.UtcNow));

        var estornoHandler = new ConsumoEstornadoHandler(produtos, movimentos, saldos, bus);
        var estornoEvento = new ConsumoEstornado("os-8", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow);

        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay

        Assert.Single(bus.Publicados.OfType<CustoBaixadoPorOs>().Where(c => c.Estornado));
    }

    [Fact]
    public async Task PecaReservada_ChamadoDuasVezes_NaoDuplicaReserva()
    {
        var (produtos, movimentos, saldos, produto) = await MontarCenarioComProdutoAsync(5, 180m);
        var bus = new FakeIntegrationEventBus();
        var handler = new PecaReservadaHandler(produtos, movimentos, saldos, bus);
        var evento = new PecaReservada("os-7", TenantId, "linha-1", produto.Id, Quantidade.DeInteiro(2).Milesimos, DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento); // replay

        var saldo = await saldos.ObterAsync(TenantId, produto.Id, "principal");
        Assert.Equal(Quantidade.DeInteiro(2), saldo!.Reservado);
    }
}
