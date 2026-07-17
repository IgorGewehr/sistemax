using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova que o extrato do Bancário (docs/wiring/financeiro-telas-restantes.md §3) junta
/// corretamente MovimentoFinanceiro + nome da forma + descrição de competência + status de
/// conciliação — nunca inventa nenhum dos quatro.</summary>
public sealed class MovimentosBancariosServiceTests
{
    private const string BusinessId = "biz-1";

    private static (MovimentosBancariosService Servico, InMemoryMovimentoFinanceiroRepository Movimentos, InMemoryFormaDePagamentoRepository Formas,
        InMemoryConciliacaoRepository Conciliacoes, InMemoryContaAReceberRepository ContasAReceber, InMemoryContaAPagarRepository ContasAPagar) CriarServico()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formas = new InMemoryFormaDePagamentoRepository();
        var conciliacoes = new InMemoryConciliacaoRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var resolvedor = new ResolvedorDeDescricaoDeMovimento(contasAReceber, contasAPagar);
        var servico = new MovimentosBancariosService(movimentos, formas, conciliacoes, resolvedor);
        return (servico, movimentos, formas, conciliacoes, contasAReceber, contasAPagar);
    }

    [Fact]
    public async Task ListarAsync_devolve_entrada_com_sinal_positivo_e_saida_com_sinal_negativo()
    {
        var (servico, movimentos, formas, _, contasAReceber, contasAPagar) = CriarServico();

        var forma = FormaDePagamento.Criar(BusinessId, "pix", TipoFormaPagamento.Pix).Valor;
        await formas.SalvarAsync(forma);

        var contaReceber = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-1"), "Venda venda-1", "servicos", DateTimeOffset.UtcNow, new Money(5_000),
            ContaFinanceiraBase.ParcelaUnica(new Money(5_000), DateTimeOffset.UtcNow)).Valor;
        await contasAReceber.SalvarAsync(contaReceber);

        var entrada = MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", forma.Id, "parcela-1", contaReceber.Id,
            TipoMovimentoFinanceiro.Entrada, new Money(5_000), DateTimeOffset.UtcNow, new SourceRef("sale-payment", "venda-1")).Valor;
        await movimentos.SalvarAsync(entrada);

        var saida = MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", forma.Id, "parcela-2", "conta-origem-2",
            TipoMovimentoFinanceiro.Saida, new Money(2_000), DateTimeOffset.UtcNow, new SourceRef("compras", "compra-1")).Valor;
        await movimentos.SalvarAsync(saida);

        var resultado = await servico.ListarAsync(BusinessId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        var linhaEntrada = Assert.Single(resultado, r => r.Id == entrada.Id);
        Assert.Equal(new Money(5_000), linhaEntrada.Valor);
        Assert.Equal("Venda venda-1", linhaEntrada.Descricao);
        Assert.Equal("pix", linhaEntrada.Forma);
        Assert.False(linhaEntrada.Conciliado);

        var linhaSaida = Assert.Single(resultado, r => r.Id == saida.Id);
        Assert.Equal(new Money(-2_000), linhaSaida.Valor);
        Assert.Equal("compras · compra-1", linhaSaida.Descricao);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_conta_quando_informado()
    {
        var (servico, movimentos, formas, _, _, _) = CriarServico();
        var forma = FormaDePagamento.Criar(BusinessId, "dinheiro", TipoFormaPagamento.Dinheiro).Valor;
        await formas.SalvarAsync(forma);

        var daContaA = MovimentoFinanceiro.Registrar(
            BusinessId, "conta-a", forma.Id, "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(1_000), DateTimeOffset.UtcNow, new SourceRef("vendas", "v1")).Valor;
        var daContaB = MovimentoFinanceiro.Registrar(
            BusinessId, "conta-b", forma.Id, "parcela-2", "origem-2",
            TipoMovimentoFinanceiro.Entrada, new Money(2_000), DateTimeOffset.UtcNow, new SourceRef("vendas", "v2")).Valor;
        await movimentos.SalvarAsync(daContaA);
        await movimentos.SalvarAsync(daContaB);

        var resultado = await servico.ListarAsync(BusinessId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), contaBancariaCaixaId: "conta-a");

        var linha = Assert.Single(resultado);
        Assert.Equal(daContaA.Id, linha.Id);
    }

    [Fact]
    public async Task ListarAsync_marca_conciliado_quando_ha_conciliacao_confirmada()
    {
        var (servico, movimentos, formas, conciliacoes, _, _) = CriarServico();
        var forma = FormaDePagamento.Criar(BusinessId, "pix", TipoFormaPagamento.Pix).Valor;
        await formas.SalvarAsync(forma);

        var movimento = MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", forma.Id, "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(1_000), DateTimeOffset.UtcNow, new SourceRef("vendas", "v1")).Valor;
        await movimentos.SalvarAsync(movimento);

        var conciliacao = Conciliacao.Criar(BusinessId, movimento.Id, "extrato-1");
        conciliacao.Confirmar(automatico: true, DateTimeOffset.UtcNow);
        await conciliacoes.SalvarAsync(conciliacao);

        var resultado = await servico.ListarAsync(BusinessId, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        Assert.True(Assert.Single(resultado).Conciliado);
    }
}
