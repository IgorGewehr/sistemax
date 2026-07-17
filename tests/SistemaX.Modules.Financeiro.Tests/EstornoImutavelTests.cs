using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// Estorno = novo fato imutável (ReversalOfId), NUNCA edição/apagamento do original
/// (docs/financeiro-datamodel.md §4.4). Cobre tanto o agregado isolado (MovimentoFinanceiro)
/// quanto a orquestração completa via EstornarMovimentoUseCase (movimento + lançamento contábil).
/// </summary>
public class EstornoImutavelTests
{
    [Fact]
    public void MovimentoFinanceiro_GerarEstorno_CriaNovoRegistroComSinalInvertido()
    {
        var original = MovimentoFinanceiro.Registrar(
            "business-1", "caixa-1", "pix", "parcela-1", "conta-1",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(300), DateTimeOffset.UtcNow, new SourceRef("sale-payment", "venda-1")).Valor;

        var estornoResultado = original.GerarEstorno(DateTimeOffset.UtcNow.AddDays(1), new SourceRef("sale-reversal", "venda-1"));

        Assert.True(estornoResultado.Sucesso);
        var estorno = estornoResultado.Valor;

        Assert.NotEqual(original.Id, estorno.Id);
        Assert.Equal(original.Id, estorno.ReversalOfId);
        Assert.Equal(TipoMovimentoFinanceiro.Saida, estorno.Tipo); // sinal invertido
        Assert.Equal(original.Valor, estorno.Valor); // mesmo valor absoluto

        // o original permanece intocado
        Assert.Equal(TipoMovimentoFinanceiro.Entrada, original.Tipo);
        Assert.Null(original.ReversalOfId);
        Assert.False(original.EhEstorno);
        Assert.True(estorno.EhEstorno);
    }

    [Fact]
    public void MovimentoFinanceiro_GerarEstornoDeEstorno_Falha()
    {
        var original = MovimentoFinanceiro.Registrar(
            "business-1", "caixa-1", "pix", "parcela-1", "conta-1",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(100), DateTimeOffset.UtcNow, new SourceRef("sale-payment", "venda-2")).Valor;
        var estorno = original.GerarEstorno(DateTimeOffset.UtcNow, new SourceRef("sale-reversal", "venda-2")).Valor;

        var resultado = estorno.GerarEstorno(DateTimeOffset.UtcNow, new SourceRef("sale-reversal-2", "venda-2"));

        Assert.True(resultado.Falha);
    }

    [Fact]
    public async Task EstornarMovimentoUseCase_ChamadoDuasVezesParaOMesmoOriginal_NaoDuplicaOEstorno()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var original = MovimentoFinanceiro.Registrar(
            "business-1", "caixa-1", "pix", "parcela-1", "conta-1",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(500), DateTimeOffset.UtcNow, new SourceRef("sale-payment", "venda-3")).Valor;
        await movimentos.SalvarAsync(original);

        var lancamentoOriginal = LancamentoContabilFactory.DeMovimentoEntrada(original).Valor;
        await lancamentos.SalvarAsync(lancamentoOriginal);

        var useCase = new EstornarMovimentoUseCase(movimentos, lancamentos);
        var origemEstorno = new SourceRef("sale-reversal", "venda-3");

        var primeiraChamada = await useCase.ExecutarAsync(original.Id, DateTimeOffset.UtcNow.AddHours(1), origemEstorno, "estorno de teste");
        var segundaChamada = await useCase.ExecutarAsync(original.Id, DateTimeOffset.UtcNow.AddHours(2), origemEstorno, "replay do mesmo evento");

        Assert.True(primeiraChamada.Sucesso);
        Assert.True(segundaChamada.Sucesso);
        Assert.Equal(primeiraChamada.Valor.Id, segundaChamada.Valor.Id); // mesmo estorno, não um segundo

        var todosOsMovimentosDoPeriodo = await movimentos.ListarPorPeriodoAsync(
            "business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, todosOsMovimentosDoPeriodo.Count); // original + 1 único estorno, nunca 3

        var todosOsLancamentosDoPeriodo = await lancamentos.ListarPorPeriodoAsync(
            "business-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(2, todosOsLancamentosDoPeriodo.Count); // lançamento original + 1 único lançamento de estorno
    }
}
