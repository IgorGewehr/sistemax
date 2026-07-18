using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.CasosDeUso;

/// <summary>
/// P1-3 + P1-6 (docs/financeiro/revisao-domain-fit-cnpj.md) — <c>BaixarParcelaUseCase</c> passa a
/// publicar <see cref="ParcelaBaixada"/> pós-commit, o insumo que fecha o gap de
/// <c>fato_caixa_diario</c> unilateral. Para ENTRADA (ContaAReceber) o valor de caixa publicado já
/// vem LÍQUIDO de MDR quando a forma de pagamento tem taxa — resolvido contra o lar único
/// <c>FormaDePagamento</c>, nunca recomputado em paralelo.
/// </summary>
public sealed class BaixarParcelaUseCaseTests
{
    private const string BusinessId = "business-1";

    [Fact]
    public async Task BaixarParcelaDeContaAPagar_PublicaParcelaBaixadaComoSaidaPeloValorIntegral()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();
        var bus = new FakeIntegrationEventBus();

        var vencimento = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        var conta = ContaAPagar.Criar(
            BusinessId, new SourceRef("teste", "folha-1"), "Folha", "despesa-com-pessoal",
            vencimento, Money.DeReais(4_500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(4_500), vencimento)).Valor;
        await contasAPagar.SalvarAsync(conta);

        var useCase = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos, formasDePagamento, bus);
        var dataPagamento = new DateTimeOffset(2026, 8, 5, 15, 0, 0, TimeSpan.Zero);
        var comando = new BaixarParcelaComando(
            conta.Id, conta.Parcelas[0].Id, Money.DeReais(4_500), dataPagamento, "conta-caixa-1", "transferencia", "baixa-folha-1");

        var resultado = await useCase.BaixarParcelaDeContaAPagarAsync(comando);
        Assert.True(resultado.Sucesso);

        var publicado = Assert.Single(bus.Publicados.OfType<ParcelaBaixada>());
        Assert.True(publicado.EhAPagar);
        Assert.Equal(Money.DeReais(4_500).Centavos, publicado.ValorCaixaCentavos);
        Assert.Equal(dataPagamento, publicado.OcorridoEm);
    }

    [Fact]
    public async Task BaixarParcelaDeContaAReceber_ComFormaSemTaxa_PublicaEntradaPeloValorIntegral()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();
        var bus = new FakeIntegrationEventBus();

        var vencimento = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var conta = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-pix-1"), "Venda", "servicos",
            vencimento, Money.DeReais(300), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(300), vencimento)).Valor;
        await contasAReceber.SalvarAsync(conta);

        var useCase = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos, formasDePagamento, bus);
        var comando = new BaixarParcelaComando(
            conta.Id, conta.Parcelas[0].Id, Money.DeReais(300), vencimento, "conta-caixa-1", "pix", "baixa-pix-1");

        await useCase.BaixarParcelaDeContaAReceberAsync(comando);

        var publicado = Assert.Single(bus.Publicados.OfType<ParcelaBaixada>());
        Assert.False(publicado.EhAPagar);
        Assert.Equal(Money.DeReais(300).Centavos, publicado.ValorCaixaCentavos); // sem cartão, MDR = 0
    }

    [Fact]
    public async Task BaixarParcelaDeContaAReceber_ComFormaComMdr_PublicaEntradaPeloValorLiquido()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var formasDePagamento = new InMemoryFormaDePagamentoRepository();
        var bus = new FakeIntegrationEventBus();

        var credito = FormaDePagamento.Criar(BusinessId, "cartao_credito", TipoFormaPagamento.Credito, taxaPercentual: 0.0349m, prazoCompensacaoDias: 30).Valor;
        await formasDePagamento.SalvarAsync(credito);

        var dataVenda = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var dataLiquidacao = dataVenda.AddDays(30);
        var conta = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-cartao-1"), "Venda", "servicos",
            dataVenda, Money.DeReais(1_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), dataLiquidacao)).Valor;
        await contasAReceber.SalvarAsync(conta);

        var useCase = new BaixarParcelaUseCase(contasAReceber, contasAPagar, movimentos, lancamentos, formasDePagamento, bus);
        var comando = new BaixarParcelaComando(
            conta.Id, conta.Parcelas[0].Id, Money.DeReais(1_000), dataLiquidacao, "conta-caixa-1", "cartao_credito", "baixa-cartao-1");

        var resultado = await useCase.BaixarParcelaDeContaAReceberAsync(comando);
        Assert.True(resultado.Sucesso);

        // A parcela em si liquida o BRUTO (o cliente pagou o ticket cheio) — MDR não é
        // inadimplência do cliente, é custo do lojista com a adquirente.
        var contaAtualizada = await contasAReceber.ObterPorIdAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Pago, contaAtualizada!.Status);

        var publicado = Assert.Single(bus.Publicados.OfType<ParcelaBaixada>());
        Assert.False(publicado.EhAPagar);
        Assert.Equal(96_510, publicado.ValorCaixaCentavos); // R$1.000 - 3,49% = R$965,10
        Assert.Equal(dataLiquidacao, publicado.OcorridoEm);

        // Regressão do gap de unidade (docs/financeiro/revisao-domain-fit-cnpj.md P1-3/P1-6): o
        // MovimentoFinanceiro registrado (fonte de IMovimentoFinanceiroRepository.CalcularSaldoAsync,
        // o "saldo atual" somado às bandas/EWMA em PrevisaoDeCaixaService) tem que ser o MESMO
        // valor líquido publicado no evento (fonte de fato_caixa_diario) — nunca bruto de um lado e
        // líquido do outro.
        Assert.Equal(96_510, resultado.Valor.Valor.Centavos);
        var saldo = await movimentos.CalcularSaldoAsync(BusinessId, null, dataLiquidacao.AddMinutes(1));
        Assert.Equal(96_510, saldo.Centavos);
    }
}
