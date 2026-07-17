using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova que o saldo da tela Bancário é <c>SaldoInicial + soma dos MovimentoFinanceiro</c>
/// — nunca um campo armazenado (docs/financeiro-datamodel.md §2.2).</summary>
public sealed class ContasBancariasServiceTests
{
    private const string BusinessId = "biz-1";

    [Fact]
    public async Task ListarAsync_soma_saldoInicial_com_movimentos_da_conta()
    {
        var contas = new InMemoryContaBancariaCaixaRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new ContasBancariasService(contas, movimentos);

        var conta = ContaBancariaCaixa.Criar(BusinessId, "Itaú PJ", TipoContaBancariaCaixa.ContaCorrente, new Money(10_000)).Valor;
        await contas.SalvarAsync(conta);

        var entrada = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(5_000), DateTimeOffset.UtcNow, new SourceRef("vendas", "venda-1")).Valor;
        await movimentos.SalvarAsync(entrada);

        var saida = MovimentoFinanceiro.Registrar(
            BusinessId, conta.Id, "forma-1", "parcela-2", "origem-2",
            TipoMovimentoFinanceiro.Saida, new Money(2_000), DateTimeOffset.UtcNow, new SourceRef("compras", "compra-1")).Valor;
        await movimentos.SalvarAsync(saida);

        var resumo = Assert.Single(await servico.ListarAsync(BusinessId));

        Assert.Equal(conta.Id, resumo.Id);
        Assert.Equal(new Money(10_000 + 5_000 - 2_000), resumo.Saldo);
    }

    [Fact]
    public async Task ListarAsync_conta_sem_movimento_mostra_apenas_o_saldo_inicial()
    {
        var contas = new InMemoryContaBancariaCaixaRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new ContasBancariasService(contas, movimentos);

        var conta = ContaBancariaCaixa.Criar(BusinessId, "Caixa", TipoContaBancariaCaixa.CaixaFisico, new Money(1_500)).Valor;
        await contas.SalvarAsync(conta);

        var resumo = Assert.Single(await servico.ListarAsync(BusinessId));
        Assert.Equal(new Money(1_500), resumo.Saldo);
    }

    [Fact]
    public async Task ListarAsync_movimento_de_outra_conta_nao_afeta_o_saldo()
    {
        var contas = new InMemoryContaBancariaCaixaRepository();
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new ContasBancariasService(contas, movimentos);

        var contaA = ContaBancariaCaixa.Criar(BusinessId, "Conta A", TipoContaBancariaCaixa.ContaCorrente).Valor;
        var contaB = ContaBancariaCaixa.Criar(BusinessId, "Conta B", TipoContaBancariaCaixa.ContaCorrente).Valor;
        await contas.SalvarAsync(contaA);
        await contas.SalvarAsync(contaB);

        var movimentoDeB = MovimentoFinanceiro.Registrar(
            BusinessId, contaB.Id, "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(9_000), DateTimeOffset.UtcNow, new SourceRef("vendas", "venda-1")).Valor;
        await movimentos.SalvarAsync(movimentoDeB);

        var resultado = await servico.ListarAsync(BusinessId);
        var resumoA = resultado.Single(r => r.Id == contaA.Id);
        var resumoB = resultado.Single(r => r.Id == contaB.Id);

        Assert.Equal(Money.Zero, resumoA.Saldo);
        Assert.Equal(new Money(9_000), resumoB.Saldo);
    }
}
