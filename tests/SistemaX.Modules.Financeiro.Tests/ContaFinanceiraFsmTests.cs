using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// FSM de status (R4-equivalente): Pago e Cancelado são terminais — nenhuma correção depois
/// disso é uma transição de status, é sempre um novo fato (estorno).
/// </summary>
public class ContaFinanceiraFsmTests
{
    [Theory]
    [InlineData(StatusFinanceiro.Pago, StatusFinanceiro.Aberto)]
    [InlineData(StatusFinanceiro.Pago, StatusFinanceiro.Cancelado)]
    [InlineData(StatusFinanceiro.Cancelado, StatusFinanceiro.Aberto)]
    [InlineData(StatusFinanceiro.Cancelado, StatusFinanceiro.Pago)]
    public void EstadosTerminais_NuncaPermitemSair(StatusFinanceiro de, StatusFinanceiro para)
    {
        var resultado = StatusFinanceiroFsm.AssertirTransicao(de, para);
        Assert.True(resultado.Falha);
    }

    [Theory]
    [InlineData(StatusFinanceiro.Aberto, StatusFinanceiro.Parcial)]
    [InlineData(StatusFinanceiro.Aberto, StatusFinanceiro.Pago)]
    [InlineData(StatusFinanceiro.Aberto, StatusFinanceiro.Atrasado)]
    [InlineData(StatusFinanceiro.Aberto, StatusFinanceiro.Cancelado)]
    [InlineData(StatusFinanceiro.Parcial, StatusFinanceiro.Pago)]
    [InlineData(StatusFinanceiro.Atrasado, StatusFinanceiro.Pago)]
    [InlineData(StatusFinanceiro.Atrasado, StatusFinanceiro.Cancelado)]
    public void TransicoesValidas_SaoPermitidas(StatusFinanceiro de, StatusFinanceiro para)
    {
        var resultado = StatusFinanceiroFsm.AssertirTransicao(de, para);
        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void RegistrarLiquidacaoParcela_AlemDoValor_Falha()
    {
        var conta = CriarContaComUmaParcela(Money.DeReais(100));

        var resultado = conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(150), DateTimeOffset.UtcNow, "pix");

        Assert.True(resultado.Falha);
        Assert.Equal(StatusFinanceiro.Aberto, conta.Status);
    }

    [Fact]
    public void RegistrarLiquidacaoParcial_DepoisTotal_TransitaAbertoParcialPago()
    {
        var conta = CriarContaComUmaParcela(Money.DeReais(100));

        var parcial = conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(40), DateTimeOffset.UtcNow, "pix");
        Assert.True(parcial.Sucesso);
        Assert.Equal(StatusFinanceiro.Parcial, conta.Status);

        var final = conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(60), DateTimeOffset.UtcNow, "pix");
        Assert.True(final.Sucesso);
        Assert.Equal(StatusFinanceiro.Pago, conta.Status);
    }

    [Fact]
    public void Cancelar_ContaComParcelaPaga_Falha()
    {
        var conta = CriarContaComUmaParcela(Money.DeReais(100));
        conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(100), DateTimeOffset.UtcNow, "pix");

        var resultado = conta.Cancelar("tentativa indevida de cancelar conta já paga");

        Assert.True(resultado.Falha);
        Assert.Equal(StatusFinanceiro.Pago, conta.Status); // Pago é terminal — cancelar não regride o status
    }

    [Fact]
    public void Cancelar_ContaAberta_Sucesso()
    {
        var conta = CriarContaComUmaParcela(Money.DeReais(100));

        var resultado = conta.Cancelar("venda estornada antes de qualquer pagamento");

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusFinanceiro.Cancelado, conta.Status);
    }

    private static ContaAReceber CriarContaComUmaParcela(Money valor)
    {
        var sourceRef = new SourceRef("teste", Guid.NewGuid().ToString());
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, DateTimeOffset.UtcNow.AddDays(30));
        return ContaAReceber.Criar("business-1", sourceRef, "Conta de teste", "servicos", DateTimeOffset.UtcNow, valor, parcelas).Valor;
    }
}
