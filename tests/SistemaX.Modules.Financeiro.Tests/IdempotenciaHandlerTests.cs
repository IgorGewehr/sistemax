using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// Regra dura R3 do datamodel: reprocessar o mesmo evento de integração é NO-OP no consumidor —
/// nunca duplica lançamento financeiro. Todo teste aqui chama o mesmo handler 2x com o MESMO
/// evento e verifica que o estado persistido é idêntico ao de uma única chamada.
/// </summary>
public class IdempotenciaHandlerTests
{
    [Fact]
    public async Task VendaConcluidaHandler_ChamadoDuasVezes_CriaApenasUmaContaEUmMovimentoEUmLancamentoDeCadaTipo()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new VendaConcluidaHandler(contasAReceber, movimentos, lancamentos);

        var evento = new VendaConcluida("venda-idempotente-1", "business-1", 15_000, "dinheiro", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento); // replay do mesmo evento — simula reentrega do bus

        var contas = await contasAReceber.ListarPorCompetenciaAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(contas);
        Assert.Equal(StatusFinanceiro.Pago, contas[0].Status); // dinheiro = à vista, nasce já quitada

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(movimentosSalvos);

        var lancamentosSalvos = await lancamentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, lancamentosSalvos.Count); // 1 de competência (ContaAReceber) + 1 de caixa (MovimentoFinanceiro)
    }

    [Fact]
    public async Task VendaConcluidaHandler_FormaAPrazo_NaoGeraMovimentoFinanceiro()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new VendaConcluidaHandler(contasAReceber, movimentos, lancamentos);

        var evento = new VendaConcluida("venda-a-prazo-1", "business-1", 20_000, "cartao_credito", DateTimeOffset.UtcNow);
        await handler.HandleAsync(evento);

        var conta = (await contasAReceber.ListarPorCompetenciaAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1))).Single();
        Assert.Equal(StatusFinanceiro.Aberto, conta.Status);

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(60));
        Assert.Empty(movimentosSalvos); // nenhum dinheiro mudou de mão ainda — só a camada de competência existe
    }

    [Fact]
    public async Task CompraRecebidaHandler_ChamadoDuasVezes_CriaApenasUmaContaAPagar()
    {
        var contasAPagar = new InMemoryContaAPagarRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new CompraRecebidaHandler(contasAPagar, lancamentos);

        var evento = new CompraRecebida("compra-1", "business-1", "fornecedor-1", 8_000, DateTimeOffset.UtcNow);

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento);

        var contas = await contasAPagar.ListarPorCompetenciaAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(contas);

        var lancamentosSalvos = await lancamentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(lancamentosSalvos);
    }

    [Fact]
    public async Task VendaEstornadaHandler_ChamadoDuasVezesAposVendaPaga_GeraApenasUmEstorno()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var vendaHandler = new VendaConcluidaHandler(contasAReceber, movimentos, lancamentos);
        var estornoHandler = new VendaEstornadaHandler(contasAReceber, movimentos, new EstornarMovimentoUseCase(movimentos, lancamentos));

        var vendaEvento = new VendaConcluida("venda-estornada-1", "business-1", 10_000, "pix", DateTimeOffset.UtcNow);
        await vendaHandler.HandleAsync(vendaEvento);

        var estornoEvento = new VendaEstornada("venda-estornada-1", "business-1", 10_000, DateTimeOffset.UtcNow.AddHours(2));
        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, movimentosSalvos.Count); // entrada original + 1 único estorno de saída

        var saldo = await movimentos.CalcularSaldoAsync("business-1", null, DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(saldo.EhZero); // entrada de 100 + estorno de -100 = líquido zero
    }

    [Fact]
    public async Task CompraEstornadaHandler_CompraAbertaSemPagamento_CancelaAContaAPagar()
    {
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var compraHandler = new CompraRecebidaHandler(contasAPagar, lancamentos);
        await compraHandler.HandleAsync(new CompraRecebida("compra-estornada-aberta", "business-1", "fornecedor-1", 8_000, DateTimeOffset.UtcNow));

        var estornoHandler = new CompraEstornadaHandler(contasAPagar, movimentos, new EstornarMovimentoUseCase(movimentos, lancamentos));
        var itens = Array.Empty<ItemMovimentado>();
        var estornoEvento = new CompraEstornada("compra-estornada-aberta", "business-1", "fornecedor-1", itens, 8_000, DateTimeOffset.UtcNow.AddHours(1));

        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay — idempotência

        var conta = await contasAPagar.BuscarPorOrigemAsync("business-1", "purchaseNote:compra-estornada-aberta");
        Assert.Equal(StatusFinanceiro.Cancelado, conta!.Status);
    }

    [Fact]
    public async Task CompraEstornadaHandler_ChamadoDuasVezesAposCompraPaga_GeraApenasUmEstorno()
    {
        var contasAPagar = new InMemoryContaAPagarRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var compraHandler = new CompraRecebidaHandler(contasAPagar, lancamentos);
        await compraHandler.HandleAsync(new CompraRecebida("compra-estornada-paga", "business-1", "fornecedor-1", 12_000, DateTimeOffset.UtcNow));

        var conta = await contasAPagar.BuscarPorOrigemAsync("business-1", "purchaseNote:compra-estornada-paga");
        var baixarParcela = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos);
        var comando = new BaixarParcelaComando(
            conta!.Id, conta.Parcelas[0].Id, Money.DeReais(120), DateTimeOffset.UtcNow, "conta-caixa-1", "boleto", "baixa-compra-1");
        await baixarParcela.BaixarParcelaDeContaAPagarAsync(comando);

        var estornoHandler = new CompraEstornadaHandler(contasAPagar, movimentos, new EstornarMovimentoUseCase(movimentos, lancamentos));
        var itens = Array.Empty<ItemMovimentado>();
        var estornoEvento = new CompraEstornada("compra-estornada-paga", "business-1", "fornecedor-1", itens, 12_000, DateTimeOffset.UtcNow.AddHours(2));

        await estornoHandler.HandleAsync(estornoEvento);
        await estornoHandler.HandleAsync(estornoEvento); // replay

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, movimentosSalvos.Count); // saída original (baixa) + 1 único estorno de entrada

        var saldo = await movimentos.CalcularSaldoAsync("business-1", null, DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(saldo.EhZero); // saída de 120 + estorno de +120 = líquido zero
    }

    [Fact]
    public async Task BaixarParcelaUseCase_ChamadoDuasVezesComMesmaIdempotencyKey_NaoDuplicaOMovimento()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var vendaHandler = new VendaConcluidaHandler(contasAReceber, movimentos, lancamentos);
        await vendaHandler.HandleAsync(new VendaConcluida("venda-a-prazo-baixa", "business-1", 12_000, "boleto", DateTimeOffset.UtcNow));

        var conta = await contasAReceber.BuscarPorOrigemAsync("business-1", "sale:venda-a-prazo-baixa");
        var comando = new BaixarParcelaComando(
            conta!.Id, conta.Parcelas[0].Id, Money.DeReais(120), DateTimeOffset.UtcNow, "conta-caixa-1", "pix", "baixa-idempotente-1");

        var baixarParcela = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos);

        var primeiraChamada = await baixarParcela.BaixarParcelaDeContaAReceberAsync(comando);
        var segundaChamada = await baixarParcela.BaixarParcelaDeContaAReceberAsync(comando); // reenvio (ex.: retry de rede do cliente)

        Assert.True(primeiraChamada.Sucesso);
        Assert.True(segundaChamada.Sucesso);
        Assert.Equal(primeiraChamada.Valor.Id, segundaChamada.Valor.Id);

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync("business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(movimentosSalvos);
    }

    [Fact]
    public async Task AvaliarParcelasVencidasUseCase_ExecutadoDuasVezesNoMesmoDia_PublicaApenasUmaVez()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var bus = new FakeIntegrationEventBus();

        var handler = new CompraRecebidaHandler(contasAPagar, lancamentos);
        var relogio = new FakeRelogio(DateTimeOffset.UtcNow);

        // compra a prazo (vencimento em +30 dias) — avança o relógio pra depois do vencimento
        await handler.HandleAsync(new CompraRecebida("compra-vencendo-1", "business-1", "fornecedor-1", 5_000, relogio.Momento));
        relogio.Momento = relogio.Momento.AddDays(31);

        var useCase = new AvaliarParcelasVencidasUseCase(contasAReceber, contasAPagar, bus, relogio);

        var publicadosPrimeiraRodada = await useCase.ExecutarAsync("business-1");
        var publicadosSegundaRodada = await useCase.ExecutarAsync("business-1"); // "roda o cron 2x no mesmo dia"

        Assert.Equal(1, publicadosPrimeiraRodada);
        Assert.Equal(0, publicadosSegundaRodada); // já estava Atrasado — nada novo a publicar
        Assert.Single(bus.Publicados);
    }
}
