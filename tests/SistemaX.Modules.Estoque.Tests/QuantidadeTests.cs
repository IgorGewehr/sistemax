using SistemaX.Modules.Estoque.Domain.Comum;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// <c>Quantidade</c> é o "Money das quantidades": milésimos-inteiros, nunca double. Estes testes
/// travam a aritmética e o arredondamento bancário que sustentam TODO o resto do módulo (saldo,
/// custo médio, curva ABC) — um bug aqui se propaga silenciosamente para todos os read-models.
/// </summary>
public class QuantidadeTests
{
    [Fact]
    public void DeDecimal_ConverteFracaoParaMilesimos()
    {
        var quantidade = Quantidade.DeDecimal(0.250m);
        Assert.Equal(250, quantidade.Milesimos);
        Assert.Equal(0.250m, quantidade.EmDecimal);
    }

    [Fact]
    public void DeInteiro_ConverteUnidadesParaMilesimos()
    {
        Assert.Equal(2000, Quantidade.DeInteiro(2).Milesimos);
    }

    [Fact]
    public void DeDecimal_UsaArredondamentoBancario()
    {
        // 0,1235 arredonda pra 0,124 (para cima) e 0,1245 pra 0,124 (para par) — ToEven.
        Assert.Equal(124, Quantidade.DeDecimal(0.1235m).Milesimos);
        Assert.Equal(124, Quantidade.DeDecimal(0.1245m).Milesimos);
    }

    [Fact]
    public void OperadoresAritmeticos_SomamESubtraemMilesimos()
    {
        var a = new Quantidade(1500);
        var b = new Quantidade(500);

        Assert.Equal(2000, (a + b).Milesimos);
        Assert.Equal(1000, (a - b).Milesimos);
        Assert.Equal(-1000, (-a + b).Milesimos);
        Assert.Equal(3000, (a * 2).Milesimos);
    }

    [Fact]
    public void ComparacoesRefletemMilesimos()
    {
        var maior = new Quantidade(2000);
        var menor = new Quantidade(1000);

        Assert.True(maior > menor);
        Assert.True(menor < maior);
        Assert.True(maior >= new Quantidade(2000));
        Assert.True(menor <= new Quantidade(1000));
    }

    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1000, false, true, false)]
    [InlineData(-1000, false, false, true)]
    public void FlagsDeSinal_RefletemMilesimos(long milesimos, bool ehZero, bool ehPositiva, bool ehNegativa)
    {
        var quantidade = new Quantidade(milesimos);
        Assert.Equal(ehZero, quantidade.EhZero);
        Assert.Equal(ehPositiva, quantidade.EhPositiva);
        Assert.Equal(ehNegativa, quantidade.EhNegativa);
    }
}
