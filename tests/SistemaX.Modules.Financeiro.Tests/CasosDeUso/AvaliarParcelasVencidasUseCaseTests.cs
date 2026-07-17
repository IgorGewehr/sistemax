using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.CasosDeUso;

/// <summary>
/// O "cron financeiro" de verdade (docs/financeiro-datamodel.md §4.2) — prova as duas invariantes
/// que a frente 1 da autonomia do motor financeiro exige: (1) vence a parcela CERTA, publicando
/// <see cref="ParcelaVencida"/> só pra quem de fato cruzou o vencimento; (2) é IDEMPOTENTE — rodar
/// duas vezes sobre o mesmo estado não duplica o evento publicado (a segunda rodada já encontra a
/// parcela em <c>Atrasado</c>, e <c>Parcela.MarcarAtrasada</c> só levanta o domain event na
/// TRANSIÇÃO, nunca ao reafirmar um estado já atingido).
/// </summary>
public sealed class AvaliarParcelasVencidasUseCaseTests
{
    private const string BusinessId = "business-1";

    [Fact]
    public async Task ExecutarAsync_ParcelaVencidaDeContaAReceber_PublicaParcelaVencidaUmaUnicaVez()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var bus = new FakeIntegrationEventBus();
        var relogio = new FakeRelogio(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        var vencimento = new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero); // 2 dias antes de "agora"
        var conta = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-1"), "Venda 1", "servicos",
            vencimento, Money.DeReais(100), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), vencimento)).Valor;
        await contasAReceber.SalvarAsync(conta);

        var useCase = new AvaliarParcelasVencidasUseCase(contasAReceber, contasAPagar, bus, relogio);

        var publicadosPrimeiraRodada = await useCase.ExecutarAsync(BusinessId);
        var publicadosSegundaRodada = await useCase.ExecutarAsync(BusinessId); // idempotência: rodar de novo não duplica

        Assert.Equal(1, publicadosPrimeiraRodada);
        Assert.Equal(0, publicadosSegundaRodada);

        var evento = Assert.Single(bus.Publicados.OfType<ParcelaVencida>());
        Assert.Equal(conta.Parcelas[0].Id, evento.ParcelaId);
        Assert.False(evento.EhAPagar);
        Assert.Equal(10_000, evento.ValorCentavos);

        var relida = await contasAReceber.ObterPorIdAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Atrasado, relida!.Parcelas[0].Status);
    }

    [Fact]
    public async Task ExecutarAsync_ParcelaVencidaDeContaAPagar_PublicaComEhAPagarVerdadeiro()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var bus = new FakeIntegrationEventBus();
        var relogio = new FakeRelogio(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        var vencimento = new DateTimeOffset(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);
        var conta = ContaAPagar.Criar(
            BusinessId, new SourceRef("purchaseNote", "compra-1"), "Compra 1", "cmv",
            vencimento, Money.DeReais(50), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(50), vencimento)).Valor;
        await contasAPagar.SalvarAsync(conta);

        var useCase = new AvaliarParcelasVencidasUseCase(contasAReceber, contasAPagar, bus, relogio);
        var publicados = await useCase.ExecutarAsync(BusinessId);

        Assert.Equal(1, publicados);
        var evento = Assert.Single(bus.Publicados.OfType<ParcelaVencida>());
        Assert.True(evento.EhAPagar);
    }

    [Fact]
    public async Task ExecutarAsync_ParcelaAindaNoPrazo_NaoPublicaNada()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var bus = new FakeIntegrationEventBus();
        var relogio = new FakeRelogio(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        var vencimento = new DateTimeOffset(2026, 1, 20, 12, 0, 0, TimeSpan.Zero); // futuro
        var conta = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-2"), "Venda 2", "servicos",
            vencimento, Money.DeReais(100), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), vencimento)).Valor;
        await contasAReceber.SalvarAsync(conta);

        var useCase = new AvaliarParcelasVencidasUseCase(contasAReceber, contasAPagar, bus, relogio);
        var publicados = await useCase.ExecutarAsync(BusinessId);

        Assert.Equal(0, publicados);
        Assert.Empty(bus.Publicados);
    }
}
