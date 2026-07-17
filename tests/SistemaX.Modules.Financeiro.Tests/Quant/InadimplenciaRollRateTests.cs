using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class InadimplenciaRollRateTests
{
    [Theory]
    [InlineData(-5, FaixaDeAtraso.EmDia)]
    [InlineData(0, FaixaDeAtraso.EmDia)]
    [InlineData(1, FaixaDeAtraso.Ate30Dias)]
    [InlineData(30, FaixaDeAtraso.Ate30Dias)]
    [InlineData(31, FaixaDeAtraso.De31a60Dias)]
    [InlineData(60, FaixaDeAtraso.De31a60Dias)]
    [InlineData(61, FaixaDeAtraso.De61a90Dias)]
    [InlineData(90, FaixaDeAtraso.De61a90Dias)]
    [InlineData(91, FaixaDeAtraso.De91a180Dias)]
    [InlineData(180, FaixaDeAtraso.De91a180Dias)]
    [InlineData(181, FaixaDeAtraso.Acima180Dias)]
    public void Classifica_faixa_pelos_limites_corretos(int diasAtraso, FaixaDeAtraso esperado)
    {
        Assert.Equal(esperado, InadimplenciaRollRate.ClassificarFaixa(diasAtraso));
    }

    [Fact]
    public void Provisao_em_dia_e_sempre_zero()
    {
        var resultado = InadimplenciaRollRate.CalcularProvisao([new InadimplenciaRollRate.ParcelaEmAberto("p1", 10_000_00, -3)]);
        Assert.Equal(0, resultado.ProvisaoEsperadaCentavos);
        Assert.Equal(10_000_00, resultado.ValorTotalEmAbertoCentavos);
    }

    [Fact]
    public void Provisao_aplica_a_taxa_padrao_por_faixa()
    {
        var resultado = InadimplenciaRollRate.CalcularProvisao([new InadimplenciaRollRate.ParcelaEmAberto("p1", 1_000_00, 45)]);
        // faixa 31-60 -> taxa 10%
        Assert.Equal(100_00, resultado.ProvisaoEsperadaCentavos);
        Assert.Equal(100_00, resultado.PorFaixa[FaixaDeAtraso.De31a60Dias].ProvisaoCentavos);
    }

    [Fact]
    public void Provisao_agrega_multiplas_parcelas_da_mesma_faixa()
    {
        var parcelas = new[]
        {
            new InadimplenciaRollRate.ParcelaEmAberto("p1", 1_000_00, 10),
            new InadimplenciaRollRate.ParcelaEmAberto("p2", 2_000_00, 20),
        };

        var resultado = InadimplenciaRollRate.CalcularProvisao(parcelas);

        Assert.Equal(3_000_00, resultado.PorFaixa[FaixaDeAtraso.Ate30Dias].ValorCentavos);
        Assert.Equal(2, resultado.PorFaixa[FaixaDeAtraso.Ate30Dias].Quantidade);
        Assert.Equal((long)Math.Round(3_000_00 * 0.02), resultado.PorFaixa[FaixaDeAtraso.Ate30Dias].ProvisaoCentavos);
    }

    [Fact]
    public void Taxas_customizadas_sobrescrevem_a_tabela_padrao()
    {
        var taxasCustom = new Dictionary<FaixaDeAtraso, double> { [FaixaDeAtraso.Ate30Dias] = 0.50 };
        var resultado = InadimplenciaRollRate.CalcularProvisao(
            [new InadimplenciaRollRate.ParcelaEmAberto("p1", 1_000_00, 10)],
            taxasCustom);

        Assert.Equal(500_00, resultado.ProvisaoEsperadaCentavos);
    }

    [Fact]
    public void Matriz_de_roll_rate_e_row_estocastica_mesmo_sem_observacoes()
    {
        var matriz = InadimplenciaRollRate.EstimarMatrizRollRate([]);

        foreach (var faixa in Enum.GetValues<FaixaDeAtraso>())
        {
            var somaDaLinha = matriz[faixa].Values.Sum();
            Assert.True(Math.Abs(somaDaLinha - 1.0) < 1e-9, $"linha {faixa} não soma 1: {somaDaLinha}");
        }
    }

    [Fact]
    public void Matriz_de_roll_rate_reflete_transicoes_observadas()
    {
        var transicoes = new List<(FaixaDeAtraso De, FaixaDeAtraso Para)>();
        for (var i = 0; i < 100; i++) transicoes.Add((FaixaDeAtraso.Ate30Dias, FaixaDeAtraso.De31a60Dias));

        var matriz = InadimplenciaRollRate.EstimarMatrizRollRate(transicoes);

        // Com 100 observações concentradas + suavização de Laplace (+1/célula), a probabilidade
        // dominante deve ser esmagadoramente para De31a60Dias.
        Assert.True(matriz[FaixaDeAtraso.Ate30Dias][FaixaDeAtraso.De31a60Dias] > 0.9);
    }
}
