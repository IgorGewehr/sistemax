using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class PrecoPorDivisorTests
{
    [Fact]
    public void Calcular_ComMdrImpostoEComissao_DivideCustoPorUmMenosSomaDosPercentuais()
    {
        // Custo R$50; MDR 3,49% (crédito) + alíquota efetiva 6% + comissão 10% = 19,49%; margem 20%.
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.0349m, 0.06m, 0.10m], 0.20m);

        Assert.NotNull(resultado);
        var divisor = 1m - 0.0349m - 0.06m - 0.10m - 0.20m;
        var esperado = (long)Math.Ceiling(5_000 / divisor);
        Assert.Equal(esperado, resultado!.PrecoSugeridoCentavos);
        Assert.Equal(0.0349m + 0.06m + 0.10m, resultado.SomaPercentuaisSobrePreco);
    }

    [Fact]
    public void Calcular_PrecoPiso_NaoCobraMargemNenhuma_ELogoENoMaximoOPrecoSugerido()
    {
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.0349m, 0.06m], margemDesejada: 0.20m);

        Assert.NotNull(resultado);
        Assert.True(resultado!.PrecoPisoCentavos < resultado.PrecoSugeridoCentavos);

        var divisorPiso = 1m - 0.0349m - 0.06m;
        Assert.Equal((long)Math.Ceiling(5_000 / divisorPiso), resultado.PrecoPisoCentavos);
    }

    /// <summary>Invariante-chave do documento (docs/financeiro/ideias-matemonstro.md): o divisor
    /// SEMPRE pede um preço maior que o markup multiplicador ingênuo (<c>Custo × (1+Σ%)</c>) para
    /// os MESMOS percentuais — é exatamente o erro que "mais quebra comércio" que a fórmula
    /// corrige.</summary>
    [Fact]
    public void Calcular_PrecoPorDivisorEhSempreMaiorQueMarkupMultiplicadorIngenuo()
    {
        const long custo = 10_000;
        decimal[] percentuais = [0.0349m, 0.06m, 0.10m];
        const decimal margem = 0.15m;

        var divisor = PrecoPorDivisor.Calcular(custo, percentuais, margem)!;
        var multiplicadorIngenuo = (long)Math.Ceiling(custo * (1 + percentuais.Sum() + margem));

        Assert.True(divisor.PrecoSugeridoCentavos > multiplicadorIngenuo);
    }

    [Fact]
    public void Calcular_SomaDePercentuaisMaisMargemMaiorOuIgualA100Porcento_DevolveNull()
    {
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.50m, 0.30m], margemDesejada: 0.25m); // soma = 105%

        Assert.Null(resultado);
    }

    [Fact]
    public void Calcular_SomaDePercentuaisExatamente100Porcento_DevolveNull_NuncaDivisaoPorZero()
    {
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.50m, 0.50m], margemDesejada: 0m);

        Assert.Null(resultado);
    }

    [Fact]
    public void Calcular_ComPrecoAtual_ExpoeAMargemRealQueOPrecoPraticadoEntrega()
    {
        // Custo R$50, 20% sobre preço, preço atual R$100 -> margem real = (1-0,20) - 50/100 = 0,30 (30%).
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.20m], margemDesejada: 0m, precoAtualCentavos: 10_000);

        Assert.NotNull(resultado);
        Assert.Equal(0.30m, resultado!.MargemRealNoPrecoAtualPercent);
    }

    [Fact]
    public void Calcular_SemPrecoAtual_MargemRealNoPrecoAtualEhNula()
    {
        var resultado = PrecoPorDivisor.Calcular(5_000, [0.20m], margemDesejada: 0.10m);

        Assert.NotNull(resultado);
        Assert.Null(resultado!.MargemRealNoPrecoAtualPercent);
    }

    [Fact]
    public void Calcular_CustoNegativo_Lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrecoPorDivisor.Calcular(-1, [], 0m));
    }
}
