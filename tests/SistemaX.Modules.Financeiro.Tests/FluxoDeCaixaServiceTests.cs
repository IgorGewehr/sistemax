using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// O read-model precisa combinar as DUAS visões corretamente: REALIZADO (soma de
/// MovimentoFinanceiro já ocorrido, regime de caixa) e PROJETADO (parcelas em aberto por
/// vencimento futuro, regime de competência ainda não liquidado) — nunca confundir uma com a
/// outra (docs/financeiro-features.md §4.1, §4.2).
/// </summary>
public class FluxoDeCaixaServiceTests
{
    [Fact]
    public async Task ProjetarAsync_SeparaRealizadoDeProjetadoCorretamente()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();

        var hoje = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var relogio = new FakeRelogio(hoje);

        // Realizado: uma entrada de caixa já ocorrida hoje.
        var movimentoResultado = MovimentoFinanceiro.Registrar(
            "business-1", "caixa-1", "pix", "parcela-realizada", "conta-realizada",
            TipoMovimentoFinanceiro.Entrada, Money.DeReais(200), hoje, new SourceRef("teste", "mov-1"));
        await movimentos.SalvarAsync(movimentoResultado.Valor);

        // Projetado: uma conta a receber com vencimento daqui a 5 dias, ainda aberta.
        var vencimentoFuturo = hoje.AddDays(5);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), vencimentoFuturo);
        var contaFutura = ContaAReceber.Criar(
            "business-1", new SourceRef("teste", "conta-futura"), "Recebível futuro", "servicos", hoje, Money.DeReais(500), parcelas).Valor;
        await contasAReceber.SalvarAsync(contaFutura);

        var service = new FluxoDeCaixaService(movimentos, contasAReceber, contasAPagar, new InMemoryFormaDePagamentoRepository(), relogio);
        var resultado = await service.ProjetarAsync("business-1", diasHistorico: 2, diasProjecao: 10);

        var pontoHoje = resultado.Pontos.Single(p => p.Data == DateOnly.FromDateTime(hoje.UtcDateTime));
        Assert.False(pontoHoje.Projetado);
        Assert.Equal(Money.DeReais(200), pontoHoje.Entradas);

        var pontoFuturo = resultado.Pontos.Single(p => p.Data == DateOnly.FromDateTime(vencimentoFuturo.UtcDateTime));
        Assert.True(pontoFuturo.Projetado);
        Assert.Equal(Money.DeReais(500), pontoFuturo.Entradas);

        // saldo acumulado no dia da projeção futura = 200 (realizado) + 500 (projetado) = 700
        Assert.Equal(Money.DeReais(700), pontoFuturo.SaldoAcumulado);
    }

    [Fact]
    public async Task ProjetarAsync_DetectaPrimeiroDiaComSaldoProjetadoNegativo()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();

        var hoje = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var relogio = new FakeRelogio(hoje);

        // Nenhum saldo em caixa hoje; uma conta a pagar vence em 3 dias — caixa fica negativo.
        var vencimento = hoje.AddDays(3);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(150), vencimento);
        var contaAPagar = ContaAPagar.Criar(
            "business-1", new SourceRef("teste", "conta-a-pagar-futura"), "Fornecedor", "cmv-fornecedor", hoje, Money.DeReais(150), parcelas).Valor;
        await contasAPagar.SalvarAsync(contaAPagar);

        var service = new FluxoDeCaixaService(movimentos, contasAReceber, contasAPagar, new InMemoryFormaDePagamentoRepository(), relogio);
        var resultado = await service.ProjetarAsync("business-1", diasHistorico: 1, diasProjecao: 10);

        Assert.NotNull(resultado.PrimeiroDiaNegativo);
        Assert.Equal(DateOnly.FromDateTime(vencimento.UtcDateTime), resultado.PrimeiroDiaNegativo);
    }

    /// <summary>
    /// P1-6(b) (docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): o restante de uma parcela
    /// PARCIALMENTE paga já tem <c>FormaPagamentoId</c> conhecido (o pagamento parcial o gravou) —
    /// a projeção do saldo restante tem que aplicar o LÍQUIDO de MDR dessa forma, não o bruto.
    /// </summary>
    [Fact]
    public async Task ProjetarAsync_RestanteDeParcelaParcialComFormaConhecida_ProjetaLiquidoDeMdr()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();

        var credito = FormaDePagamento.Criar("business-1", "cartao_credito", TipoFormaPagamento.Credito, taxaPercentual: 0.0349m, prazoCompensacaoDias: 30).Valor;
        await formasDePagamento.SalvarAsync(credito);

        var hoje = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero);
        var relogio = new FakeRelogio(hoje);
        var vencimentoFuturo = hoje.AddDays(5);

        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), vencimentoFuturo);
        var conta = ContaAReceber.Criar(
            "business-1", new SourceRef("teste", "conta-parcial"), "Venda parcelada", "servicos", hoje, Money.DeReais(1_000), parcelas).Valor;

        // Pagamento parcial hoje com cartão de crédito — grava FormaPagamentoId na parcela e deixa
        // R$800 em aberto para o vencimento futuro.
        var liquidacaoParcial = conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(200), hoje, "cartao_credito");
        Assert.True(liquidacaoParcial.Sucesso);
        await contasAReceber.SalvarAsync(conta);

        var restanteBruto = Money.DeReais(800);
        var restanteLiquidoEsperado = credito.CalcularValorLiquido(restanteBruto);

        var service = new FluxoDeCaixaService(movimentos, contasAReceber, contasAPagar, formasDePagamento, relogio);
        var resultado = await service.ProjetarAsync("business-1", diasHistorico: 1, diasProjecao: 10);

        var pontoFuturo = resultado.Pontos.Single(p => p.Data == DateOnly.FromDateTime(vencimentoFuturo.UtcDateTime));
        Assert.Equal(restanteLiquidoEsperado, pontoFuturo.Entradas);
        Assert.NotEqual(restanteBruto, pontoFuturo.Entradas); // regressão: não pode projetar o bruto quando a forma é conhecida
    }
}
