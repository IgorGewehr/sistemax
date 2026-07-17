using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// A invariante mais dura do módulo: Σdébito == Σcrédito dentro de todo LancamentoContabil,
/// garantida no PORTÃO ÚNICO de criação (LancamentoContabil.Criar). Nenhum caminho de código
/// consegue instanciar um lançamento desbalanceado — não há setter público, não há construtor
/// público, só o factory validado.
/// </summary>
public class LancamentoContabilTests
{
    private static readonly OrigemLancamento OrigemTeste = new("financeiro", "teste", "id-1");

    [Fact]
    public void Criar_ComPartidasBalanceadas_RetornaSucessoEBate()
    {
        Money valor = new(50_000); // R$ 500,00
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, valor),
            PartidaContabil.Credito(PlanoDeContasPadrao.Receita.Id, valor)
        };

        var resultado = LancamentoContabil.Criar(
            "business-1", DateTimeOffset.UtcNow, "Venda de teste", OrigemTeste, partidas);

        Assert.True(resultado.Sucesso);
        Assert.Equal(resultado.Valor.TotalDebito, resultado.Valor.TotalCredito);
        Assert.Equal(valor, resultado.Valor.TotalDebito);
    }

    [Fact]
    public void Criar_ComPartidasDesbalanceadas_RetornaFalhaEDetectaOBug()
    {
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, new Money(50_000)),
            PartidaContabil.Credito(PlanoDeContasPadrao.Receita.Id, new Money(30_000)) // propositalmente errado
        };

        var resultado = LancamentoContabil.Criar(
            "business-1", DateTimeOffset.UtcNow, "Lançamento com bug no gerador", OrigemTeste, partidas);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.lancamento.desbalanceado", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_SoComDebitos_RetornaFalha()
    {
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, new Money(10_000)),
            PartidaContabil.Debito(PlanoDeContasPadrao.CaixaEBancos.Id, new Money(10_000))
        };

        var resultado = LancamentoContabil.Criar("business-1", DateTimeOffset.UtcNow, "Só débito", OrigemTeste, partidas);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.lancamento.partidas_insuficientes", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComPartidaDeValorZeroOuNegativo_RetornaFalha()
    {
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.ContasAReceber.Id, Money.Zero),
            PartidaContabil.Credito(PlanoDeContasPadrao.Receita.Id, Money.Zero)
        };

        var resultado = LancamentoContabil.Criar("business-1", DateTimeOffset.UtcNow, "Valor zero", OrigemTeste, partidas);

        Assert.True(resultado.Falha);
    }

    [Fact]
    public void GerarEstorno_ProduzLancamentoBalanceadoComPartidasInvertidas()
    {
        Money valor = new(12_345);
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.CaixaEBancos.Id, valor),
            PartidaContabil.Credito(PlanoDeContasPadrao.ContasAReceber.Id, valor)
        };
        var original = LancamentoContabil.Criar("business-1", DateTimeOffset.UtcNow, "Recebimento", OrigemTeste, partidas).Valor;

        var estornoResultado = original.GerarEstorno(DateTimeOffset.UtcNow.AddDays(1), "cliente pediu reembolso");

        Assert.True(estornoResultado.Sucesso);
        var estorno = estornoResultado.Valor;

        Assert.Equal(estorno.TotalDebito, estorno.TotalCredito); // ainda balanceado
        Assert.Equal(original.Id, estorno.ReversalOfId);
        Assert.NotEqual(original.Id, estorno.Id);

        // partidas espelhadas: onde o original tinha débito, o estorno tem crédito, e vice-versa
        var debitoOriginal = original.Partidas.Single(p => p.Natureza == NaturezaPartida.Debito);
        var creditoEstornoNaMesmaConta = estorno.Partidas.Single(p => p.ContaContabilId == debitoOriginal.ContaContabilId);
        Assert.Equal(NaturezaPartida.Credito, creditoEstornoNaMesmaConta.Natureza);
        Assert.Equal(debitoOriginal.Valor, creditoEstornoNaMesmaConta.Valor);

        // o original NUNCA muda — mesma referência, mesmos totais
        Assert.Equal(valor, original.TotalDebito);
        Assert.False(original.EhEstorno);
        Assert.True(estorno.EhEstorno);
    }

    [Fact]
    public void GerarEstorno_DeUmEstorno_RetornaFalha()
    {
        var partidas = new[]
        {
            PartidaContabil.Debito(PlanoDeContasPadrao.CaixaEBancos.Id, new Money(1_000)),
            PartidaContabil.Credito(PlanoDeContasPadrao.ContasAReceber.Id, new Money(1_000))
        };
        var original = LancamentoContabil.Criar("business-1", DateTimeOffset.UtcNow, "Original", OrigemTeste, partidas).Valor;
        var estorno = original.GerarEstorno(DateTimeOffset.UtcNow, "motivo").Valor;

        var estornoDeEstorno = estorno.GerarEstorno(DateTimeOffset.UtcNow, "motivo 2");

        Assert.True(estornoDeEstorno.Falha);
        Assert.Equal("financeiro.lancamento.estorno_de_estorno", estornoDeEstorno.Erro.Codigo);
    }
}
