using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

/// <summary>
/// Cenário nominal do dia-zero (docs/financeiro/design-imobilizado-roi.md §7.8): capex R$55.800 no
/// mês 0, burn de R$3.000/mês nos meses 1-4, depois F=+R$6.000/mês → PaybackSimples no mês 16.
/// </summary>
public sealed class MatematicaDePaybackTests
{
    private static List<(DateOnly Competencia, long LiquidoCentavos)> SerieNominal()
    {
        var serie = new List<(DateOnly, long)> { (new DateOnly(2026, 7, 1), -5_580_000) }; // N_0
        for (var m = 1; m <= 4; m++) serie.Add((new DateOnly(2026, 7, 1).AddMonths(m), -300_000)); // N_1..4
        for (var m = 5; m <= 24; m++) serie.Add((new DateOnly(2026, 7, 1).AddMonths(m), 600_000)); // N_5..
        return serie;
    }

    [Fact]
    public void PaybackSimples_CenarioNominal_CruzaNoMes16()
    {
        var serie = SerieNominal();
        var payback = MatematicaDePayback.PaybackSimples(serie);

        // mês 0 = jul/2026 → mês 16 = nov/2027
        Assert.Equal(new DateOnly(2027, 11, 1), payback);
    }

    [Fact]
    public void PaybackSimples_semCruzamento_retorna_null()
    {
        var serie = new List<(DateOnly, long)>
        {
            (new DateOnly(2026, 7, 1), -1_000_000),
            (new DateOnly(2026, 8, 1), -100_000),
            (new DateOnly(2026, 9, 1), -100_000),
        };

        Assert.Null(MatematicaDePayback.PaybackSimples(serie));
    }

    [Fact]
    public void PaybackSimples_fluxoSempreNaoNegativo_nunca_ficou_negativo_retorna_null()
    {
        // Nunca cruzou de negativo pra não-negativo — não há "payback a realizar" (design §7.3).
        var serie = new List<(DateOnly, long)>
        {
            (new DateOnly(2026, 7, 1), 100_000),
            (new DateOnly(2026, 8, 1), 100_000),
        };

        Assert.Null(MatematicaDePayback.PaybackSimples(serie));
    }

    [Fact]
    public void PaybackDescontado_ComTaxaPositiva_e_maior_ou_igual_ao_simples_em_fluxo_canonico()
    {
        var serie = SerieNominal();
        var simples = MatematicaDePayback.PaybackSimples(serie);
        var descontado = MatematicaDePayback.PaybackDescontado(serie, taxaMensal: 0.01m); // ~12,68% a.a.

        Assert.NotNull(simples);
        Assert.NotNull(descontado);
        Assert.True(descontado >= simples, $"Descontado ({descontado}) deveria ser >= simples ({simples}).");
    }

    [Fact]
    public void PaybackDescontado_ComTaxaZero_e_igual_ao_simples()
    {
        var serie = SerieNominal();
        var simples = MatematicaDePayback.PaybackSimples(serie);
        var descontado = MatematicaDePayback.PaybackDescontado(serie, taxaMensal: 0m);

        Assert.Equal(simples, descontado);
    }

    [Fact]
    public void ProjetarCruzamento_acumuladoJaPositivo_retorna_zero()
    {
        var resultado = MatematicaDePayback.ProjetarCruzamento(100, _ => -10, 120);
        Assert.Equal(0, resultado);
    }

    [Fact]
    public void ProjetarCruzamento_cruza_no_mes_esperado()
    {
        // Acumulado -1000, margem +250/mês → cruza no mês 4 (−1000+4×250=0).
        var resultado = MatematicaDePayback.ProjetarCruzamento(-1000, _ => 250, 120);
        Assert.Equal(4, resultado);
    }

    [Fact]
    public void ProjetarCruzamento_naoCruzaNoHorizonte_retorna_null()
    {
        var resultado = MatematicaDePayback.ProjetarCruzamento(-1_000_000, _ => 1, 12);
        Assert.Null(resultado);
    }

    [Fact]
    public void ProjetarCruzamento_decimal_espelha_o_overload_long()
    {
        var resultado = MatematicaDePayback.ProjetarCruzamento(-1000m, _ => 250m, 120);
        Assert.Equal(4, resultado);
    }
}
