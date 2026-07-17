using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>
/// Reproduz o cenário canônico de docs/financeiro-datamodel.md §3 — venda a prazo de 30 dias: o
/// MESMO fato de negócio produz DOIS registros com datas diferentes, nunca um só. Competência
/// nasce no dia da venda; caixa só nasce no dia do pagamento efetivo.
/// </summary>
public class CaixaVsCompetenciaTests
{
    [Fact]
    public async Task VendaAPrazo_CompetenciaNasceNaVenda_CaixaSoNasceNoPagamento()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var diaDaVenda = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var diaDoPagamento = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

        var vendaHandler = new VendaConcluidaHandler(contasAReceber, movimentos, lancamentos);
        await vendaHandler.HandleAsync(new VendaConcluida("venda-a-prazo-competencia", "business-1", 30_000, "cartao_credito", diaDaVenda));

        // --- Dia 01: só a visão de COMPETÊNCIA existe ---
        var conta = await contasAReceber.BuscarPorOrigemAsync("business-1", "sale:venda-a-prazo-competencia");
        Assert.NotNull(conta);
        Assert.Equal(diaDaVenda, conta!.DataCompetencia);
        Assert.Equal(StatusFinanceiro.Aberto, conta.Status); // ainda não pago

        var saldoNoDiaDaVenda = await movimentos.CalcularSaldoAsync("business-1", null, diaDaVenda);
        Assert.True(saldoNoDiaDaVenda.EhZero); // nenhum dinheiro mudou de mão ainda

        var dreDoMesDaVenda = conta.ValorTotal;
        Assert.Equal(Money.DeReais(300), dreDoMesDaVenda); // DRE de competência já reconheceria os R$300

        // --- Dia 31: cliente paga via PIX — nasce a visão de CAIXA ---
        var baixarParcela = new BaixarParcelaUseCase(contasAReceber, new InMemoryContaAPagarRepository(), movimentos, lancamentos);
        var parcelaId = conta.Parcelas[0].Id;

        var pagamentoResultado = await baixarParcela.BaixarParcelaDeContaAReceberAsync(new BaixarParcelaComando(
            conta.Id, parcelaId, Money.DeReais(300), diaDoPagamento, "conta-caixa-1", "pix", "baixa-venda-a-prazo-1"));

        Assert.True(pagamentoResultado.Sucesso);

        var contaAtualizada = await contasAReceber.ObterPorIdAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Pago, contaAtualizada!.Status);

        var saldoAntesDoPagamento = await movimentos.CalcularSaldoAsync("business-1", null, diaDoPagamento.AddSeconds(-1));
        Assert.True(saldoAntesDoPagamento.EhZero);

        var saldoDepoisDoPagamento = await movimentos.CalcularSaldoAsync("business-1", null, diaDoPagamento);
        Assert.Equal(Money.DeReais(300), saldoDepoisDoPagamento); // caixa realizado só reconhece agora
    }
}
