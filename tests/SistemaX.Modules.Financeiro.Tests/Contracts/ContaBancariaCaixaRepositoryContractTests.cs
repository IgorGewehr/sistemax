using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IContaBancariaCaixaRepository"/> — roda o MESMO
/// conjunto de casos contra <c>InMemoryContaBancariaCaixaRepository</c> e
/// <c>SqliteContaBancariaCaixaRepository</c>.</summary>
public abstract class ContaBancariaCaixaRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IContaBancariaCaixaRepository CriarRepositorio();

    private static ContaBancariaCaixa CriarConta(string businessId, string nome, TipoContaBancariaCaixa tipo = TipoContaBancariaCaixa.ContaCorrente, long saldoInicialCentavos = 0)
        => ContaBancariaCaixa.Criar(businessId, nome, tipo, new Money(saldoInicialCentavos)).Valor;

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_conta()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "Itaú PJ", TipoContaBancariaCaixa.ContaCorrente, 812_000);

        await repo.SalvarAsync(conta);
        var lida = await repo.ObterPorIdAsync(BusinessA, conta.Id);

        Assert.NotNull(lida);
        Assert.Equal(conta.Id, lida!.Id);
        Assert.Equal(conta.BusinessId, lida.BusinessId);
        Assert.Equal(conta.Nome, lida.Nome);
        Assert.Equal(conta.Tipo, lida.Tipo);
        Assert.Equal(conta.SaldoInicial, lida.SaldoInicial);
        Assert.Equal(conta.Ativa, lida.Ativa);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, "conta-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "Caixa loja");
        await repo.SalvarAsync(conta);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, conta.Id));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_desativar_reflete_o_novo_estado()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "Nubank PJ");
        await repo.SalvarAsync(conta);

        conta.Desativar();
        await repo.SalvarAsync(conta);

        var lida = await repo.ObterPorIdAsync(BusinessA, conta.Id);
        Assert.False(lida!.Ativa);
    }

    [Fact]
    public async Task ListarAsync_retorna_apenas_do_business_pedido()
    {
        var repo = CriarRepositorio();
        var daLojaA = CriarConta(BusinessA, "Conta A");
        var daLojaB = CriarConta(BusinessB, "Conta B");

        await repo.SalvarAsync(daLojaA);
        await repo.SalvarAsync(daLojaB);

        var lista = await repo.ListarAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(daLojaA.Id, lista[0].Id);
    }

    [Fact]
    public async Task ListarAsync_apenasAtivas_filtra_as_inativas()
    {
        var repo = CriarRepositorio();
        var ativa = CriarConta(BusinessA, "Ativa");
        var inativa = CriarConta(BusinessA, "Inativa");
        inativa.Desativar();

        await repo.SalvarAsync(ativa);
        await repo.SalvarAsync(inativa);

        var lista = await repo.ListarAsync(BusinessA, apenasAtivas: true);

        Assert.Single(lista);
        Assert.Equal(ativa.Id, lista[0].Id);
    }

    [Fact]
    public async Task Criar_com_id_explicito_persiste_com_o_mesmo_id()
    {
        // A semente idempotente pina o id da conta-caixa padrão (mesmo id que
        // ClassificadorFormaPagamento.ContaCaixaPadraoId já usava como referência hardcoded em
        // MovimentoFinanceiro) — sem isso, o saldo derivado nunca bateria com o ledger existente.
        var repo = CriarRepositorio();
        var conta = ContaBancariaCaixa.Criar(BusinessA, "Caixa", TipoContaBancariaCaixa.CaixaFisico, id: "conta-caixa-padrao").Valor;

        await repo.SalvarAsync(conta);
        var lida = await repo.ObterPorIdAsync(BusinessA, "conta-caixa-padrao");

        Assert.NotNull(lida);
        Assert.Equal("conta-caixa-padrao", lida!.Id);
    }
}
