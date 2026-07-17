using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova que o extrato unificado de Entradas &amp; Saídas (docs/wiring/
/// financeiro-telas-restantes.md §1/§A) junta REALIZADO (MovimentoFinanceiro) com PREVISTO/
/// ATRASADO (Parcela em aberto), soma os KPIs certo e nunca vaza entre tenants (R1).</summary>
public sealed class ExtratoUnificadoServiceTests
{
    private const string BusinessId = "biz-1";
    private const string OutroBusinessId = "biz-2";
    private static readonly DateTimeOffset Hoje = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    private static (
        ExtratoUnificadoService Servico, InMemoryMovimentoFinanceiroRepository Movimentos,
        InMemoryContaAReceberRepository ContasAReceber, InMemoryContaAPagarRepository ContasAPagar,
        InMemoryContaBancariaCaixaRepository ContasBancarias, InMemoryFormaDePagamentoRepository Formas) CriarServico()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var contasBancarias = new InMemoryContaBancariaCaixaRepository();
        var formas = new InMemoryFormaDePagamentoRepository();
        var relogio = new FakeRelogio(Hoje);
        var servico = new ExtratoUnificadoService(movimentos, contasAReceber, contasAPagar, contasBancarias, formas, relogio);
        return (servico, movimentos, contasAReceber, contasAPagar, contasBancarias, formas);
    }

    [Fact]
    public async Task ListarAsync_SomaTotalEntradasSaidasESaldoDoPeriodo()
    {
        var (servico, movimentos, contasAReceber, contasAPagar, contasBancarias, formas) = CriarServico();

        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú PJ", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contasBancarias.SalvarAsync(conta);
        var forma = FormaDePagamento.Criar(BusinessId, "pix", TipoFormaPagamento.Pix).Valor;
        await formas.SalvarAsync(forma);

        // Realizado: 1 entrada paga de 1.000 e 1 saída paga de 400, dentro do período. A liquidação
        // da Parcela (competência) e o MovimentoFinanceiro (caixa) são as DUAS escritas que
        // BaixarParcelaUseCase orquestra atomicamente — replicadas aqui para a conta não continuar
        // aparecendo como "em aberto" (senão o extrato duplicaria a linha).
        var receber = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v1"), "Venda avulsa", "servicos", Hoje.AddDays(-5), new Money(1_000),
            ContaFinanceiraBase.ParcelaUnica(new Money(1_000), Hoje.AddDays(-5))).Valor;
        var parcelaReceber = receber.Parcelas[0].Id;
        receber.RegistrarLiquidacaoParcela(parcelaReceber, new Money(1_000), Hoje.AddDays(-1), forma.Id);
        await contasAReceber.SalvarAsync(receber);
        var entradaPaga = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, forma.Id, parcelaReceber, receber.Id, TipoMovimentoFinanceiro.Entrada,
            new Money(1_000), Hoje.AddDays(-1), new SourceRef("sale-payment", "v1")).Valor;
        await movimentos.SalvarAsync(entradaPaga);

        var pagar = ContaAPagar.Criar(
            BusinessId, new SourceRef("compra", "c1"), "Fornecedor X", "cmv-fornecedor", Hoje.AddDays(-5), new Money(400),
            ContaFinanceiraBase.ParcelaUnica(new Money(400), Hoje.AddDays(-5))).Valor;
        var parcelaPagar = pagar.Parcelas[0].Id;
        pagar.RegistrarLiquidacaoParcela(parcelaPagar, new Money(400), Hoje.AddDays(-1), forma.Id);
        await contasAPagar.SalvarAsync(pagar);
        var saidaPaga = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, forma.Id, parcelaPagar, pagar.Id, TipoMovimentoFinanceiro.Saida,
            new Money(400), Hoje.AddDays(-1), new SourceRef("compra-payment", "c1")).Valor;
        await movimentos.SalvarAsync(saidaPaga);

        // Previsto: uma conta a receber ainda em aberto, vencendo dentro do período.
        var previsto = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v2"), "Contrato mensal", "servicos", Hoje, new Money(500),
            ContaFinanceiraBase.ParcelaUnica(new Money(500), Hoje.AddDays(3))).Valor;
        await contasAReceber.SalvarAsync(previsto);

        var resultado = await servico.ListarAsync(BusinessId, Hoje.AddDays(-10), Hoje.AddDays(10));

        Assert.Equal(3, resultado.Linhas.Count);
        Assert.Equal(new Money(1_500), resultado.Kpis.TotalEntradas); // 1.000 pago + 500 previsto
        Assert.Equal(new Money(400), resultado.Kpis.TotalSaidas);
        Assert.Equal(new Money(1_100), resultado.Kpis.SaldoPeriodo);

        var linhaPaga = Assert.Single(resultado.Linhas, l => l.Id == entradaPaga.Id);
        Assert.Equal("pago", linhaPaga.Status);
        Assert.Equal("Itaú PJ", linhaPaga.Conta);
        Assert.Equal("pix", linhaPaga.Origem);

        var linhaPrevista = Assert.Single(resultado.Linhas, l => l.Status == "previsto");
        Assert.Equal("Contrato mensal", linhaPrevista.Descricao);
        Assert.Null(linhaPrevista.Conta);
    }

    [Fact]
    public async Task ListarAsync_RespeitaPeriodo_ExcluiParcelaComVencimentoForaDaJanela()
    {
        var (servico, _, contasAReceber, _, _, _) = CriarServico();

        var dentroDoPeriodo = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v1"), "Dentro", "servicos", Hoje, new Money(100),
            ContaFinanceiraBase.ParcelaUnica(new Money(100), Hoje.AddDays(2))).Valor;
        await contasAReceber.SalvarAsync(dentroDoPeriodo);

        var foraDoPeriodo = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v2"), "Fora", "servicos", Hoje, new Money(999),
            ContaFinanceiraBase.ParcelaUnica(new Money(999), Hoje.AddDays(90))).Valor;
        await contasAReceber.SalvarAsync(foraDoPeriodo);

        var resultado = await servico.ListarAsync(BusinessId, Hoje, Hoje.AddDays(30));

        Assert.Single(resultado.Linhas);
        Assert.Equal("Dentro", resultado.Linhas[0].Descricao);
        Assert.Equal(new Money(100), resultado.Kpis.TotalEntradas);
    }

    [Fact]
    public async Task ListarAsync_FiltraPorTipoSemAlterarOsKpis()
    {
        var (servico, _, contasAReceber, contasAPagar, _, _) = CriarServico();

        var receber = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v1"), "Entrada", "servicos", Hoje, new Money(700),
            ContaFinanceiraBase.ParcelaUnica(new Money(700), Hoje.AddDays(1))).Valor;
        await contasAReceber.SalvarAsync(receber);

        var pagar = ContaAPagar.Criar(
            BusinessId, new SourceRef("compra", "c1"), "Saída", "cmv-fornecedor", Hoje, new Money(300),
            ContaFinanceiraBase.ParcelaUnica(new Money(300), Hoje.AddDays(1))).Valor;
        await contasAPagar.SalvarAsync(pagar);

        var resultado = await servico.ListarAsync(BusinessId, Hoje, Hoje.AddDays(10), tipo: "entrada");

        Assert.Single(resultado.Linhas);
        Assert.Equal("entrada", resultado.Linhas[0].Tipo);
        // KPIs continuam somando os dois tipos, mesmo com o filtro de tipo aplicado só às linhas.
        Assert.Equal(new Money(700), resultado.Kpis.TotalEntradas);
        Assert.Equal(new Money(300), resultado.Kpis.TotalSaidas);
    }

    [Fact]
    public async Task ListarAsync_NuncaVazaContaDeOutroBusinessId()
    {
        var (servico, _, contasAReceber, _, _, _) = CriarServico();

        var doOutroTenant = ContaAReceber.Criar(
            OutroBusinessId, new SourceRef("sale", "v1"), "Não deve aparecer", "servicos", Hoje, new Money(1_000),
            ContaFinanceiraBase.ParcelaUnica(new Money(1_000), Hoje.AddDays(1))).Valor;
        await contasAReceber.SalvarAsync(doOutroTenant);

        var resultado = await servico.ListarAsync(BusinessId, Hoje, Hoje.AddDays(10));

        Assert.Empty(resultado.Linhas);
        Assert.Equal(Money.Zero, resultado.Kpis.TotalEntradas);
    }

    [Fact]
    public async Task ListarAsync_MarcaAtrasadoComDiasDeAtraso()
    {
        var (servico, _, contasAReceber, _, _, _) = CriarServico();

        var vencido = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "v1"), "Mensalidade João", "servicos", Hoje.AddDays(-10), new Money(430),
            ContaFinanceiraBase.ParcelaUnica(new Money(430), Hoje.AddDays(-4))).Valor;
        vencido.AvaliarVencimento(Hoje);
        await contasAReceber.SalvarAsync(vencido);

        var resultado = await servico.ListarAsync(BusinessId, Hoje.AddDays(-30), Hoje.AddDays(30));

        var linha = Assert.Single(resultado.Linhas);
        Assert.Equal("atrasado", linha.Status);
        Assert.Equal(4, linha.DiasAtraso);
    }
}
