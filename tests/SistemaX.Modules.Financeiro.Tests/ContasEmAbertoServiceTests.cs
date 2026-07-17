using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova o card "Contas em aberto" de Relatórios (docs/wiring/
/// financeiro-telas-restantes.md §5/§B): soma certo o que está pendente e distribui o atrasado
/// nos 3 baldes de aging sem perder nem duplicar centavo — os baldes sempre somam exatamente o
/// total atrasado.</summary>
public sealed class ContasEmAbertoServiceTests
{
    private const string BusinessId = "biz-1";
    private const string OutroBusinessId = "biz-2";
    private static readonly DateTimeOffset Hoje = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private static (ContasEmAbertoService Servico, InMemoryContaAReceberRepository ContasAReceber, InMemoryContaAPagarRepository ContasAPagar) CriarServico()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var servico = new ContasEmAbertoService(contasAReceber, contasAPagar, new FakeRelogio(Hoje));
        return (servico, contasAReceber, contasAPagar);
    }

    [Fact]
    public async Task CalcularAsync_SomaReceberEPagarEmAberto_IgnorandoPagasECanceladas()
    {
        var (servico, contasAReceber, contasAPagar) = CriarServico();

        var aberta = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v1"), "Em aberto", "servicos", Hoje, new Money(900),
            ContaFinanceiraBase.ParcelaUnica(new Money(900), Hoje.AddDays(10))).Valor;
        await contasAReceber.SalvarAsync(aberta);

        var jaPaga = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v2"), "Já paga", "servicos", Hoje, new Money(5_000),
            ContaFinanceiraBase.ParcelaUnica(new Money(5_000), Hoje.AddDays(5))).Valor;
        jaPaga.RegistrarLiquidacaoParcela(jaPaga.Parcelas[0].Id, new Money(5_000), Hoje, "forma-1");
        await contasAReceber.SalvarAsync(jaPaga);

        var pagarAberta = ContaAPagar.Criar(
            BusinessId, new SourceRef("compra", "c1"), "Fornecedor", "cmv-fornecedor", Hoje, new Money(700),
            ContaFinanceiraBase.ParcelaUnica(new Money(700), Hoje.AddDays(15))).Valor;
        await contasAPagar.SalvarAsync(pagarAberta);

        var resultado = await servico.CalcularAsync(BusinessId);

        Assert.Equal(new Money(900), resultado.ReceberEmAberto);
        Assert.Equal(new Money(700), resultado.PagarEmAberto);
        Assert.Equal(Money.Zero, resultado.ReceberAtrasado);
    }

    [Fact]
    public async Task CalcularAsync_DistribuiAtrasadoNosBaldesDeAgingSemPerderCentavo()
    {
        var (servico, contasAReceber, _) = CriarServico();

        var atraso10d = CriarReceberAtrasada(BusinessId, "10 dias", new Money(900), Hoje.AddDays(-10));
        var atraso20d = CriarReceberAtrasada(BusinessId, "20 dias", new Money(600), Hoje.AddDays(-20));
        var atraso40d = CriarReceberAtrasada(BusinessId, "40 dias", new Money(390), Hoje.AddDays(-40));
        await contasAReceber.SalvarAsync(atraso10d);
        await contasAReceber.SalvarAsync(atraso20d);
        await contasAReceber.SalvarAsync(atraso40d);

        var resultado = await servico.CalcularAsync(BusinessId);

        Assert.Equal(new Money(1_890), resultado.ReceberAtrasado);
        Assert.Equal(new Money(1_890), resultado.ReceberEmAberto);

        var somaDosBaldes = resultado.AgingBuckets.Aggregate(Money.Zero, (acc, b) => acc + b.Valor);
        Assert.Equal(resultado.ReceberAtrasado, somaDosBaldes);

        Assert.Equal(new Money(900), resultado.AgingBuckets.Single(b => b.Id == "0-15").Valor);
        Assert.Equal(new Money(600), resultado.AgingBuckets.Single(b => b.Id == "15-30").Valor);
        Assert.Equal(new Money(390), resultado.AgingBuckets.Single(b => b.Id == "30+").Valor);
    }

    [Fact]
    public async Task CalcularAsync_NuncaVazaContaDeOutroBusinessId()
    {
        var (servico, contasAReceber, _) = CriarServico();

        var doOutroTenant = ContaAReceber.Criar(
            OutroBusinessId, new SourceRef("sale", "v1"), "Não deve contar", "servicos", Hoje, new Money(10_000),
            ContaFinanceiraBase.ParcelaUnica(new Money(10_000), Hoje.AddDays(5))).Valor;
        await contasAReceber.SalvarAsync(doOutroTenant);

        var resultado = await servico.CalcularAsync(BusinessId);

        Assert.Equal(Money.Zero, resultado.ReceberEmAberto);
    }

    private static ContaAReceber CriarReceberAtrasada(string businessId, string descricao, Money valor, DateTimeOffset vencimento)
    {
        var conta = ContaAReceber.Criar(
            businessId, new SourceRef("sale", descricao), descricao, "servicos", vencimento, valor,
            ContaFinanceiraBase.ParcelaUnica(valor, vencimento)).Valor;
        conta.AvaliarVencimento(Hoje);
        return conta;
    }
}
