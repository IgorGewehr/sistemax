using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova os "3 baldes" de conciliação da tela Bancário
/// (docs/wiring/financeiro-telas-restantes.md §3): bateu certinho / sobrou no banco / sobrou no
/// sistema, com a sugestão heurística de match por valor+data mais próxima.</summary>
public sealed class ConciliacaoBancariaServiceTests
{
    private const string BusinessId = "biz-1";
    private static readonly DateTimeOffset Inicio = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2026, 7, 31, 23, 59, 59, TimeSpan.Zero);

    private static (ConciliacaoBancariaService Servico, InMemoryContaBancariaCaixaRepository Contas, InMemoryExtratoBancarioItemRepository Itens,
        InMemoryConciliacaoRepository Conciliacoes, InMemoryMovimentoFinanceiroRepository Movimentos) CriarServico()
    {
        var contas = new InMemoryContaBancariaCaixaRepository();
        var itens = new InMemoryExtratoBancarioItemRepository();
        var conciliacoes = new InMemoryConciliacaoRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var resolvedor = new ResolvedorDeDescricaoDeMovimento(contasAReceber, contasAPagar);
        var servico = new ConciliacaoBancariaService(contas, itens, conciliacoes, movimentos, resolvedor);
        return (servico, contas, itens, conciliacoes, movimentos);
    }

    [Fact]
    public async Task CalcularAsync_item_confirmado_conta_como_bateu_certinho()
    {
        var (servico, contas, itens, conciliacoes, movimentos) = CriarServico();
        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contas.SalvarAsync(conta);

        var item = ExtratoBancarioItem.Importar(BusinessId, conta.Id, new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero), new Money(1_000), "PIX recebido", "ext-1");
        await itens.SalvarAsync(item);

        var movimento = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(1_000), new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v1")).Valor;
        await movimentos.SalvarAsync(movimento);

        var conciliacao = Conciliacao.Criar(BusinessId, movimento.Id, item.Id);
        conciliacao.Confirmar(automatico: true, DateTimeOffset.UtcNow);
        await conciliacoes.SalvarAsync(conciliacao);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(1, resumo.BateuCertinhoTotal);
        Assert.Single(resumo.BateuCertinhoAmostra, a => a.Descricao == "PIX recebido");
        Assert.Empty(resumo.SobrouNoBanco);
        Assert.Empty(resumo.SobrouNoSistema);
    }

    [Fact]
    public async Task CalcularAsync_item_sem_conciliacao_sugere_o_movimento_de_mesmo_valor_e_data_mais_proxima()
    {
        var (servico, contas, itens, _, movimentos) = CriarServico();
        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contas.SalvarAsync(conta);

        var item = ExtratoBancarioItem.Importar(BusinessId, conta.Id, new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero), new Money(620), "PIX recebido", "ext-2");
        await itens.SalvarAsync(item);

        var movimentoLonge = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(620), new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v1")).Valor;
        var movimentoPerto = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-2", "origem-2",
            TipoMovimentoFinanceiro.Entrada, new Money(620), new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v2")).Valor;
        await movimentos.SalvarAsync(movimentoLonge);
        await movimentos.SalvarAsync(movimentoPerto);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        var pendente = Assert.Single(resumo.SobrouNoBanco);
        Assert.Equal(item.Id, pendente.Id);
        Assert.Equal(movimentoPerto.Id, pendente.IdSugerido);
        Assert.NotNull(pendente.Sugestao);
    }

    [Fact]
    public async Task CalcularAsync_movimento_sem_extrato_correspondente_sobra_no_sistema_sem_sugestao()
    {
        var (servico, contas, _, _, movimentos) = CriarServico();
        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contas.SalvarAsync(conta);

        var movimento = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Saida, new Money(480), new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero), new SourceRef("compras", "c1")).Valor;
        await movimentos.SalvarAsync(movimento);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        var pendente = Assert.Single(resumo.SobrouNoSistema);
        Assert.Equal(movimento.Id, pendente.Id);
        Assert.Equal(new Money(-480), pendente.Valor);
        Assert.Null(pendente.IdSugerido);
        Assert.Null(pendente.Sugestao);
    }

    [Fact]
    public async Task CalcularAsync_item_ignorado_nao_aparece_em_nenhum_balde()
    {
        var (servico, contas, itens, conciliacoes, _) = CriarServico();
        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contas.SalvarAsync(conta);

        var item = ExtratoBancarioItem.Importar(BusinessId, conta.Id, new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero), new Money(38), "Tarifa", "ext-3");
        await itens.SalvarAsync(item);

        var conciliacao = Conciliacao.Criar(BusinessId, "movimento-inexistente", item.Id);
        conciliacao.Ignorar();
        await conciliacoes.SalvarAsync(conciliacao);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Empty(resumo.SobrouNoBanco);
        Assert.Equal(0, resumo.BateuCertinhoTotal);
    }
}
