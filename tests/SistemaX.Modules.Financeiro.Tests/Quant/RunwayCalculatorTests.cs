using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class RunwayCalculatorTests
{
    [Fact]
    public void Ewma_de_burn_ignora_dias_positivos_e_so_acumula_a_fracao_negativa()
    {
        // Todos os dias positivos -> burn é zero o tempo todo.
        var ewma = RunwayCalculator.CalcularBurnDiarioEwma([100, 200, 50], janela: 14);
        Assert.Equal(0, ewma);
    }

    [Fact]
    public void Ewma_de_burn_com_saidas_constantes_converge_para_o_valor_constante()
    {
        var deltas = Enumerable.Repeat(-1_000L, 60).ToArray();
        var ewma = RunwayCalculator.CalcularBurnDiarioEwma(deltas, janela: 14);
        Assert.True(Math.Abs(ewma - 1_000) < 1, $"esperava convergir perto de 1000, obteve {ewma}");
    }

    [Fact]
    public void Ewma_de_burn_primeiro_dia_e_o_proprio_burn_do_dia()
    {
        var ewma = RunwayCalculator.CalcularBurnDiarioEwma([-500]);
        Assert.Equal(500, ewma);
    }

    [Fact]
    public void Janela_invalida_lanca_excecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RunwayCalculator.CalcularBurnDiarioEwma([-100], janela: 0));
    }

    [Fact]
    public void Runway_bruto_e_saldo_dividido_pelo_burn()
    {
        var resultado = RunwayCalculator.Calcular(saldoAtualCentavos: 10_000, burnDiarioEwmaCentavos: 500, primeiroDiaP50NegativoOffset: null);
        Assert.Equal(20, resultado.DiasRunwayBruto);
        Assert.Null(resultado.DiasRunwayRealista);
    }

    [Fact]
    public void Runway_bruto_e_infinito_sem_burn_positivo()
    {
        var resultado = RunwayCalculator.Calcular(saldoAtualCentavos: 10_000, burnDiarioEwmaCentavos: 0, primeiroDiaP50NegativoOffset: null);
        Assert.Null(resultado.DiasRunwayBruto);
    }

    [Fact]
    public void Runway_realista_espelha_o_primeiro_dia_negativo_da_banda_p50()
    {
        var resultado = RunwayCalculator.Calcular(saldoAtualCentavos: 10_000, burnDiarioEwmaCentavos: 100, primeiroDiaP50NegativoOffset: 17);
        Assert.Equal(17, resultado.DiasRunwayRealista);
    }

    [Fact]
    public void Runway_nunca_e_negativo_mesmo_com_saldo_ja_no_vermelho()
    {
        var resultado = RunwayCalculator.Calcular(saldoAtualCentavos: -500, burnDiarioEwmaCentavos: 100, primeiroDiaP50NegativoOffset: 0);
        Assert.Equal(0, resultado.DiasRunwayBruto);
        Assert.Equal(0, resultado.DiasRunwayRealista);
    }
}
