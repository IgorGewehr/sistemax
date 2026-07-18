using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// P1-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — coerência de ponta a ponta: com
/// <c>fato_caixa_diario</c> agora bilateral, <see cref="PrevisaoDeCaixaService"/> (que já consumia
/// o fato sem mudar nenhuma linha de Quant — <c>RunwayCalculator</c>/<c>BandasDeFluxoDeCaixa</c>
/// sempre estiveram corretos "dado o insumo") passa a enxergar queima de caixa de verdade. ANTES
/// desta fatia, o histórico só tinha entradas — burn EWMA ficava sempre zero e o runway bruto
/// sempre <c>null</c> ("sem queima de caixa"), mesmo com folha/fornecedor consumindo caixa todo mês.
/// </summary>
public sealed class PrevisaoDeCaixaServiceTests
{
    private const string BusinessId = "business-1";
    private static readonly DateTimeOffset Hoje = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HistoricoComSaidasPeriodicas_BurnEwmaPositivoERunwayBrutoDeixaDeSerNull()
    {
        var fatoCaixa = new InMemoryFatoCaixaDiarioRepository();
        await PopularHistoricoBilateralAsync(fatoCaixa);

        var resultado = await CriarServico(fatoCaixa).CalcularAsync(BusinessId, diasProjecao: 60);

        // Regressão do gap documentado (P1-3): sem saída no insumo, isto era SEMPRE null.
        Assert.NotNull(resultado.DiasRunwayBruto);
    }

    [Fact]
    public async Task HistoricoSoComEntradas_SemQueima_RunwayBrutoContinuaNull()
    {
        var fatoCaixa = new InMemoryFatoCaixaDiarioRepository();
        await PopularHistoricoSoEntradasAsync(fatoCaixa);

        var resultado = await CriarServico(fatoCaixa).CalcularAsync(BusinessId, diasProjecao: 60);

        Assert.Null(resultado.DiasRunwayBruto); // sem burn (nenhum dia negativo no histórico), runway é infinito por definição
    }

    /// <summary>
    /// Mesma seed, mesmo saldo, mesmo fluxo conhecido (nenhum) — a ÚNICA diferença entre os dois
    /// cenários é o histórico de ruído bilateral vs. só-entradas. Como o bloco de reamostragem é
    /// sorteado com a MESMA sequência determinística nos dois (mesma seed, mesmo tamanho de
    /// histórico), cada simulação bilateral domina ponto-a-ponto a equivalente só-entradas — logo o
    /// percentil P5 (o mais pessimista) tem que ser estritamente menor com saídas no histórico.
    /// </summary>
    [Fact]
    public async Task BandaP5ComHistoricoBilateral_EMenorQueP5SoComEntradas()
    {
        var fatoCaixaBilateral = new InMemoryFatoCaixaDiarioRepository();
        await PopularHistoricoBilateralAsync(fatoCaixaBilateral);

        var fatoCaixaSoEntradas = new InMemoryFatoCaixaDiarioRepository();
        await PopularHistoricoSoEntradasAsync(fatoCaixaSoEntradas);

        var resultadoBilateral = await CriarServico(fatoCaixaBilateral).CalcularAsync(BusinessId, diasProjecao: 60);
        var resultadoSoEntradas = await CriarServico(fatoCaixaSoEntradas).CalcularAsync(BusinessId, diasProjecao: 60);

        var p5Bilateral = resultadoBilateral.Bandas[29].P5Centavos; // dia 30 de projeção
        var p5SoEntradas = resultadoSoEntradas.Bandas[29].P5Centavos;

        Assert.True(
            p5Bilateral < p5SoEntradas,
            $"P5 bilateral ({p5Bilateral}) deveria ser mais pessimista (menor) que P5 só-entradas ({p5SoEntradas}).");
    }

    /// <summary>
    /// P1-6(b) (docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): o fluxo CONHECIDO futuro
    /// entra na simulação como delta constante por dia (<c>BandasDeFluxoDeCaixa.conhecidoPorDia</c>)
    /// — com histórico de ruído VAZIO (nenhum <c>fato_caixa_diario</c>), o P50 do dia de vencimento
    /// é EXATAMENTE o delta conhecido daquele dia, sem interferência de amostragem. Isso permite
    /// verificar que o restante de uma parcela com forma de pagamento já conhecida (pagamento
    /// parcial já registrado) entra no fluxo conhecido em LÍQUIDO de MDR, não em bruto.
    /// </summary>
    [Fact]
    public async Task CalcularAsync_RestanteDeParcelaParcialComFormaConhecida_UsaLiquidoNoFluxoConhecido()
    {
        var businessId = "business-fluxo-conhecido";
        var fatoCaixa = new InMemoryFatoCaixaDiarioRepository(); // sem histórico → ruído sempre 0
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();

        var credito = FormaDePagamento.Criar(businessId, "cartao_credito", TipoFormaPagamento.Credito, taxaPercentual: 0.0349m, prazoCompensacaoDias: 30).Valor;
        await formasDePagamento.SalvarAsync(credito);

        var vencimentoFuturo = DateOnly.FromDateTime(Hoje.UtcDateTime).AddDays(5).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), vencimentoFuturo);
        var conta = ContaAReceber.Criar(
            businessId, new SourceRef("teste", "conta-parcial"), "Venda parcelada", "servicos", Hoje, Money.DeReais(1_000), parcelas).Valor;

        var liquidacaoParcial = conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(200), Hoje, "cartao_credito");
        Assert.True(liquidacaoParcial.Sucesso);
        await contasAReceber.SalvarAsync(conta);

        var restanteLiquidoEsperado = credito.CalcularValorLiquido(Money.DeReais(800));

        var service = new PrevisaoDeCaixaService(fatoCaixa, contasAReceber, contasAPagar, movimentos, formasDePagamento, new FakeRelogio(Hoje));
        var resultado = await service.CalcularAsync(businessId, diasProjecao: 10);

        var bandaNoVencimento = resultado.Bandas.Single(b => b.Data == DateOnly.FromDateTime(vencimentoFuturo));
        Assert.Equal(restanteLiquidoEsperado.Centavos, bandaNoVencimento.P50Centavos);
    }

    private static PrevisaoDeCaixaService CriarServico(IFatoCaixaDiarioRepository fatoCaixa)
        => new(
            fatoCaixa,
            new InMemoryContaAReceberRepository(),
            new InMemoryContaAPagarRepository(),
            new InMemoryMovimentoFinanceiroRepository(),
            new InMemoryFormaDePagamentoRepository(),
            new FakeRelogio(Hoje));

    /// <summary>60 dias de histórico: R$500/dia de venda à vista + R$4.000 de saída (folha/fornecedor)
    /// a cada 7 dias — o padrão real de "queima periódica" que a F0 unilateral nunca via.</summary>
    private static async Task PopularHistoricoBilateralAsync(IFatoCaixaDiarioRepository fatoCaixa)
    {
        var hojeDia = DateOnly.FromDateTime(Hoje.UtcDateTime);
        for (var i = 1; i <= 60; i++)
        {
            var dia = hojeDia.AddDays(-i);
            await fatoCaixa.AcumularEntradaAsync(BusinessId, dia, 50_000);
            if (i % 7 == 0)
                await fatoCaixa.AcumularSaidaAsync(BusinessId, dia, 400_000);
        }
    }

    private static async Task PopularHistoricoSoEntradasAsync(IFatoCaixaDiarioRepository fatoCaixa)
    {
        var hojeDia = DateOnly.FromDateTime(Hoje.UtcDateTime);
        for (var i = 1; i <= 60; i++)
        {
            var dia = hojeDia.AddDays(-i);
            await fatoCaixa.AcumularEntradaAsync(BusinessId, dia, 50_000);
        }
    }
}
