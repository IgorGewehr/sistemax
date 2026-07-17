using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// P0-2/P0-5/P1-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — OS chega ao Financeiro ponta a
/// ponta: <c>ContaAReceber</c> nasce na corrente Servico com forma/cliente preservados e, quando a
/// OS foi paga na entrega (a assistência não tem "a prazo" — <c>OrdemDeServico.Entregar</c> exige
/// forma de pagamento), LIQUIDA na hora — nunca vira um recebível "Aberto" que o cron de vencidas
/// marcaria "Atrasado" no dia seguinte (inadimplente fantasma). Espelho exato dos testes de
/// <c>VendaConcluidaHandler</c> em <see cref="IdempotenciaHandlerTests"/>.
/// </summary>
public class OsFaturadaHandlerTests
{
    private const string TenantId = "business-1";

    [Fact]
    public async Task OsFaturada_ComFormaDePagamento_CriaContaAReceberServicoJaLiquidadaComCaixaELancamentos()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new OsFaturadaHandler(contasAReceber, movimentos, lancamentos);

        var evento = new OsFaturada(
            "os-a-vista-1", TenantId, ValorServicoCentavos: 12_000, ValorPecasCentavos: 3_000, DateTimeOffset.UtcNow,
            FormaPagamento: "pix", ClienteId: "cliente-1", TecnicoId: "tecnico-1", NumeroOs: "OS-0001");

        await handler.HandleAsync(evento);

        var conta = await contasAReceber.BuscarPorOrigemAsync(TenantId, "appointment:os-a-vista-1");
        Assert.NotNull(conta);
        Assert.Equal(StatusFinanceiro.Pago, conta!.Status); // à vista — liquidada na hora, NUNCA fica "Aberto"
        Assert.Equal(CorrenteDeReceita.Servico, conta.Corrente);
        Assert.Equal("cliente-1", conta.ClienteId);
        Assert.Equal(15_000, conta.ValorTotal.Centavos); // mão de obra + peças
        Assert.Equal("tecnico-1", conta.TecnicoId); // P1-7 — dá pra consultar quem faturou a OS
        Assert.Equal(12_000, conta.ValorServico!.Value.Centavos); // repartição persistida (P1-7)
        Assert.Equal(3_000, conta.ValorPecas!.Value.Centavos);

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(movimentosSalvos); // dinheiro mudou de mão — caixa registrado

        var lancamentosSalvos = await lancamentos.ListarPorPeriodoAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, lancamentosSalvos.Count); // 1 de competência (ContaAReceber) + 1 de caixa (MovimentoFinanceiro)
    }

    [Fact]
    public async Task OsFaturada_SemFormaDePagamento_FicaAbertaSemMovimentoDeCaixa()
    {
        // Único caso legítimo de "a prazo": taxa de diagnóstico de DevolverSemReparo — a OS não
        // tem forma de pagamento porque o cliente ainda não pagou nada (o equipamento nem foi
        // reparado). Fica Aberta de verdade, com vencimento — o cron de vencidas decide depois.
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new OsFaturadaHandler(contasAReceber, movimentos, lancamentos);

        var evento = new OsFaturada("os-diagnostico-1", TenantId, ValorServicoCentavos: 5_000, ValorPecasCentavos: 0, DateTimeOffset.UtcNow);
        await handler.HandleAsync(evento);

        var conta = await contasAReceber.BuscarPorOrigemAsync(TenantId, "appointment:os-diagnostico-1");
        Assert.NotNull(conta);
        Assert.Equal(StatusFinanceiro.Aberto, conta!.Status);
        Assert.Equal(evento.OcorridoEm, conta.Parcelas[0].Vencimento); // vencimento correto, não "atrasado" no ato
        Assert.Null(conta.TecnicoId); // OS de diagnóstico sem TecnicoId no evento — permanece null

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Empty(movimentosSalvos); // nenhum dinheiro mudou de mão ainda
    }

    [Fact]
    public async Task OsFaturada_ChamadoDuasVezes_NaoDuplicaContaNemMovimentoNemLancamento()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var handler = new OsFaturadaHandler(contasAReceber, movimentos, lancamentos);

        var evento = new OsFaturada(
            "os-idempotente-1", TenantId, ValorServicoCentavos: 8_000, ValorPecasCentavos: 2_000, DateTimeOffset.UtcNow,
            FormaPagamento: "dinheiro", ClienteId: "cliente-2");

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento); // replay do mesmo evento — simula reentrega do bus (faturar 2x a mesma OS)

        var contas = await contasAReceber.ListarPorCompetenciaAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(contas);

        var movimentosSalvos = await movimentos.ListarPorPeriodoAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Single(movimentosSalvos);

        var lancamentosSalvos = await lancamentos.ListarPorPeriodoAsync(TenantId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, lancamentosSalvos.Count);
    }
}
