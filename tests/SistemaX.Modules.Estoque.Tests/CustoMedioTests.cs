using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.Modules.Estoque.Domain.Saldos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>Custo médio móvel — plano §3.5: <c>novoCM = (Fisico×CM + Qtd×CustoEntrada) / (Fisico+Qtd)</c>,
/// e <c>Fisico ≤ 0</c> zera a história (adota o custo da entrada).</summary>
public class CustoMedioTests
{
    [Fact]
    public void Recalcular_PrimeiraEntrada_AdotaCustoDaEntrada()
    {
        var novoCusto = CalculadoraDeCustoMedio.Recalcular(Quantidade.Zero, Money.Zero, Quantidade.DeInteiro(10), Money.DeReais(5));
        Assert.Equal(Money.DeReais(5), novoCusto);
    }

    [Fact]
    public void Recalcular_SegundaEntradaComCustoDiferente_PonderaPelaQuantidade()
    {
        // 10 un a R$5,00 + 10 un a R$7,00 = 20 un a R$6,00 (média simples porque as quantidades são iguais).
        var novoCusto = CalculadoraDeCustoMedio.Recalcular(Quantidade.DeInteiro(10), Money.DeReais(5), Quantidade.DeInteiro(10), Money.DeReais(7));
        Assert.Equal(Money.DeReais(6), novoCusto);
    }

    [Fact]
    public void Recalcular_ComFisicoZeroOuNegativo_ZeraHistoricoEAdotaCustoDaEntrada()
    {
        var novoCusto = CalculadoraDeCustoMedio.Recalcular(new Quantidade(-2000), Money.DeReais(999), Quantidade.DeInteiro(5), Money.DeReais(8));
        Assert.Equal(Money.DeReais(8), novoCusto);
    }

    [Fact]
    public void SaldoDeItem_AplicarSaida_NaoAlteraCustoMedio()
    {
        var saldo = SaldoDeItem.Vazio("tenant-1", "produto-1", "principal");

        var entrada = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(10), Money.DeReais(5),
            new SourceRef("manual", "m1"), "chave-entrada", "compra", "op", "Operador", DateTimeOffset.UtcNow).Valor;
        saldo.AplicarMovimento(entrada);

        var saida = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Saida, Quantidade.DeInteiro(3), saldo.CustoMedio,
            new SourceRef("venda", "v1"), "chave-saida", "venda", "op", "Operador", DateTimeOffset.UtcNow).Valor;
        saldo.AplicarMovimento(saida);

        Assert.Equal(Money.DeReais(5), saldo.CustoMedio);
        Assert.Equal(Quantidade.DeInteiro(7), saldo.Fisico);
    }

    [Fact]
    public void SaldoDeItem_ValorTotal_EhFisicoVezesCustoMedio()
    {
        var saldo = SaldoDeItem.Vazio("tenant-1", "produto-1", "principal");
        var entrada = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(4), Money.DeReais(10.50m),
            new SourceRef("manual", "m1"), "chave-1", "compra", "op", "Operador", DateTimeOffset.UtcNow).Valor;

        saldo.AplicarMovimento(entrada);

        Assert.Equal(Money.DeReais(42), saldo.ValorTotal);
    }
}
