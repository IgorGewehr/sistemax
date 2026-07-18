using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// P2-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — concentração de receita por cliente: risco
/// de dependência de conta grande.
/// </summary>
public sealed class ConcentracaoDeReceitaServiceTests
{
    private const string BusinessId = "biz-concentracao";
    private static readonly DateTimeOffset Inicio = new(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Fim = new(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(-3));

    [Fact]
    public async Task CalcularAsync_ComUmClienteDominante_ConcentracaoReflete70PorCento()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var servico = new ConcentracaoDeReceitaService(contasAReceber);

        var data = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));

        // Cliente A: R$700,00 (70% do total).
        var contaA = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-a"), "Venda", "servicos", data,
            Money.DeReais(700), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(700), data),
            clienteId: "cliente-a").Valor;
        await contasAReceber.SalvarAsync(contaA);

        // Cliente B: R$300,00 (30% do total).
        var contaB = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-b"), "Venda", "servicos", data,
            Money.DeReais(300), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(300), data),
            clienteId: "cliente-b").Valor;
        await contasAReceber.SalvarAsync(contaB);

        var resultado = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(100_000, resultado.ReceitaTotalCentavos);
        Assert.NotNull(resultado.MaiorCliente);
        Assert.Equal("cliente-a", resultado.MaiorCliente!.ClienteId);
        Assert.Equal(0.70, resultado.ConcentracaoNoMaiorClientePercentual!.Value, precision: 10);
        Assert.Equal(2, resultado.TopClientes.Count);
    }

    /// <summary>Receita anônima (sem cliente identificado, ex.: balcão) entra no TOTAL mas dilui a
    /// concentração — o risco de dependência é relativo ao faturamento inteiro.</summary>
    [Fact]
    public async Task CalcularAsync_ComReceitaAnonima_DiluiAConcentracaoSemAtribuirANinguem()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var servico = new ConcentracaoDeReceitaService(contasAReceber);

        var data = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));

        var contaCliente = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-cliente"), "Venda", "servicos", data,
            Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), data),
            clienteId: "cliente-a").Valor;
        await contasAReceber.SalvarAsync(contaCliente);

        var contaAnonima = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-balcao"), "Venda balcão", "servicos", data,
            Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), data)).Valor; // sem clienteId
        await contasAReceber.SalvarAsync(contaAnonima);

        var resultado = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(100_000, resultado.ReceitaTotalCentavos);
        Assert.Equal(50_000, resultado.ReceitaIdentificadaPorClienteCentavos); // só o cliente-a
        Assert.Equal(0.50, resultado.ConcentracaoNoMaiorClientePercentual!.Value, precision: 10); // 500/1000, não 500/500
    }

    [Fact]
    public async Task CalcularAsync_SemReceitaNenhuma_DevolveNeutroSemCrash()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var servico = new ConcentracaoDeReceitaService(contasAReceber);

        var resultado = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(0, resultado.ReceitaTotalCentavos);
        Assert.Null(resultado.MaiorCliente);
        Assert.Null(resultado.ConcentracaoNoMaiorClientePercentual);
        Assert.Empty(resultado.TopClientes);
    }
}
