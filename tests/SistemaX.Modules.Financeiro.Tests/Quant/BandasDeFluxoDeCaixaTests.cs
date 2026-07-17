using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class BandasDeFluxoDeCaixaTests
{
    [Fact]
    public void Mesma_seed_e_mesmos_insumos_produzem_resultado_identico()
    {
        var historico = new long[] { 100, -50, 200, -300, 150, -20, 400 };
        var seed = 42;

        var resultado1 = BandasDeFluxoDeCaixa.Simular(historico, 10_000, [], 30, seed);
        var resultado2 = BandasDeFluxoDeCaixa.Simular(historico, 10_000, [], 30, seed);

        Assert.Equal(resultado1.ProbabilidadeSaldoNegativoEm30Dias, resultado2.ProbabilidadeSaldoNegativoEm30Dias);
        Assert.Equal(resultado1.PrimeiroDiaOffsetP50Negativo, resultado2.PrimeiroDiaOffsetP50Negativo);
        for (var i = 0; i < resultado1.Bandas.Count; i++)
        {
            Assert.Equal(resultado1.Bandas[i], resultado2.Bandas[i]);
        }
    }

    [Fact]
    public void Sem_historico_as_bandas_colapsam_no_caminho_conhecido_deterministico()
    {
        var conhecido = new[]
        {
            new BandasDeFluxoDeCaixa.PontoConhecido(1, 1_000),
            new BandasDeFluxoDeCaixa.PontoConhecido(2, -500),
        };

        var resultado = BandasDeFluxoDeCaixa.Simular([], 10_000, conhecido, 2, seed: 1);

        Assert.Equal(11_000, resultado.Bandas[0].P5Centavos);
        Assert.Equal(11_000, resultado.Bandas[0].P50Centavos);
        Assert.Equal(11_000, resultado.Bandas[0].P95Centavos);
        Assert.Equal(10_500, resultado.Bandas[1].P50Centavos);
    }

    [Fact]
    public void P5_e_sempre_menor_ou_igual_a_p50_que_e_menor_ou_igual_a_p95()
    {
        var historico = new long[] { 500, -1_200, 300, -100, 800, -2_000, 100, 50, -300 };
        var resultado = BandasDeFluxoDeCaixa.Simular(historico, 5_000, [], 45, seed: 7);

        foreach (var banda in resultado.Bandas)
        {
            Assert.True(banda.P5Centavos <= banda.P50Centavos, $"dia {banda.DiaOffset}: P5 > P50");
            Assert.True(banda.P50Centavos <= banda.P95Centavos, $"dia {banda.DiaOffset}: P50 > P95");
        }
    }

    [Fact]
    public void Saldo_alto_e_historico_so_positivo_nunca_cruza_negativo()
    {
        var historico = new long[] { 100, 200, 50, 300 };
        var resultado = BandasDeFluxoDeCaixa.Simular(historico, 1_000_000, [], 30, seed: 3);

        Assert.Equal(0, resultado.ProbabilidadeSaldoNegativoEm30Dias);
        Assert.Null(resultado.PrimeiroDiaOffsetP50Negativo);
    }

    [Fact]
    public void Burn_severo_sem_receita_cruza_negativo_com_probabilidade_alta()
    {
        var historico = new long[] { -1_000, -900, -1_100, -950, -1_050 };
        var resultado = BandasDeFluxoDeCaixa.Simular(historico, 2_000, [], 30, seed: 9);

        Assert.True(resultado.ProbabilidadeSaldoNegativoEm30Dias > 0.9);
        Assert.NotNull(resultado.PrimeiroDiaOffsetP50Negativo);
    }

    [Fact]
    public void Retorna_uma_banda_por_dia_de_projecao()
    {
        var resultado = BandasDeFluxoDeCaixa.Simular([100, -50], 1_000, [], 15, seed: 1);
        Assert.Equal(15, resultado.Bandas.Count);
        Assert.Equal(Enumerable.Range(1, 15), resultado.Bandas.Select(b => b.DiaOffset));
    }

    [Fact]
    public void Dias_de_projecao_invalido_lanca_excecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BandasDeFluxoDeCaixa.Simular([100], 1_000, [], 0, seed: 1));
    }
}
