using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Tests;

/// <summary>
/// O algoritmo de rateio (plano §6.1) é o coração do custo de entrada — estes testes cobrem
/// especificamente o arredondamento HOSTIL (residual que não divide exato pelos itens) e a
/// invariante de ouro: Σ landed == vNF sempre, em centavos exatos.
/// </summary>
public class CustoDeEntradaTests
{
    [Fact]
    public void Ratear_FreteNaoDivisivelPorTresItens_UltimoItemAbsorveOResiduo()
    {
        var totais = TotaisDaNota.Criar(vProdCentavos: 30_000, vNfCentavos: 31_000, vFreteCentavos: 1_000).Valor;
        var itens = new[]
        {
            ComprasTestBuilder.Item(1, 10_000),
            ComprasTestBuilder.Item(2, 10_000),
            ComprasTestBuilder.Item(3, 10_000)
        };

        var resultado = CustoDeEntrada.Ratear(totais, itens);

        Assert.True(resultado.Sucesso);
        Assert.Equal(new Money(10_333), resultado.Valor[0]);
        Assert.Equal(new Money(10_333), resultado.Valor[1]);
        Assert.Equal(new Money(10_334), resultado.Valor[2]); // absorve o centavo que sobrou do rateio

        var soma = resultado.Valor.Aggregate(Money.Zero, (acc, m) => acc + m);
        Assert.Equal(totais.VNf, soma); // Σ landed == vNF, EXATO
    }

    [Fact]
    public void Ratear_ComDescontoIpiEIcmsSt_ComponeCadaItemCorretamente()
    {
        // vProd 10000 + IPI 200 + ST 100 - desconto 50 = 10250 landed (item único, sem frete)
        var totais = TotaisDaNota.Criar(vProdCentavos: 10_000, vNfCentavos: 10_250).Valor;
        var item = ComprasTestBuilder.Item(1, 10_000, vDescCentavos: 50, vIpiCentavos: 200, vIcmsStCentavos: 100);

        var resultado = CustoDeEntrada.Ratear(totais, [item]);

        Assert.True(resultado.Sucesso);
        Assert.Equal(new Money(10_250), resultado.Valor[0]);
    }

    [Fact]
    public void Ratear_ComAcessorioInformadoNoItem_NaoEntraNoResidualDeNovo()
    {
        // Item 1 já tem R$6,00 de frete informado nele; frete total da nota é R$10,00 → o
        // residual (R$4,00) é o que sobra pra ratear entre os itens pelo vProd.
        // vNf = vProd(20000) + frete(1000) = 21000.
        var totais = TotaisDaNota.Criar(vProdCentavos: 20_000, vNfCentavos: 21_000, vFreteCentavos: 1_000).Valor;
        var item1 = ComprasTestBuilder.Item(1, 10_000, vFreteItem: new Money(600));
        var item2 = ComprasTestBuilder.Item(2, 10_000);

        var resultado = CustoDeEntrada.Ratear(totais, [item1, item2]);

        Assert.True(resultado.Sucesso);
        // residual = 1000 - 600 = 400; share 50/50 (vProd iguais) → 200 cada
        // item1 = 10000 + 600 (informado) + 200 (residual) = 10800
        // item2 = vNf - item1 = 21000 - 10800 = 10200
        Assert.Equal(new Money(10_800), resultado.Valor[0]);
        Assert.Equal(new Money(10_200), resultado.Valor[1]);
    }

    [Fact]
    public void Ratear_SemItens_Falha()
    {
        var totais = TotaisDaNota.Criar(vProdCentavos: 10_000, vNfCentavos: 10_000).Valor;

        var resultado = CustoDeEntrada.Ratear(totais, []);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.custo.sem_itens", resultado.Erro.Codigo);
    }

    [Fact]
    public void Ratear_ItemUnico_LandedIgualAoVNf()
    {
        var totais = TotaisDaNota.Criar(vProdCentavos: 9_640, vNfCentavos: 9_640).Valor;
        var item = ComprasTestBuilder.Item(1, 9_640);

        var resultado = CustoDeEntrada.Ratear(totais, [item]);

        Assert.Equal(new Money(9_640), resultado.Valor[0]);
    }
}
