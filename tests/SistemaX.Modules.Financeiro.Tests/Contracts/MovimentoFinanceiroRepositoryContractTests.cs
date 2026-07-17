using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IMovimentoFinanceiroRepository"/>. Sem caso de "mutar e
/// resalvar" — <see cref="MovimentoFinanceiro"/> é IMUTÁVEL por invariante (corrigir é
/// <c>GerarEstorno</c>, um novo movimento) — em vez disso, cobrimos
/// <see cref="IMovimentoFinanceiroRepository.BuscarEstornoDeAsync"/>.
/// </summary>
public abstract class MovimentoFinanceiroRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IMovimentoFinanceiroRepository CriarRepositorio();

    private static MovimentoFinanceiro CriarMovimento(
        string businessId, string origemId, TipoMovimentoFinanceiro tipo, Money valor, DateTimeOffset dataMovimento,
        string contaBancariaCaixaId = "caixa-1", string contaOrigemId = "conta-origem-1", CorrenteDeReceita? corrente = null)
        => MovimentoFinanceiro.Registrar(
            businessId, contaBancariaCaixaId, "pix", "parcela-1", contaOrigemId,
            tipo, valor, dataMovimento, new SourceRef("vendas", origemId), corrente).Valor;

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_o_mesmo_movimento()
    {
        var repo = CriarRepositorio();
        var movimento = CriarMovimento(BusinessA, "origem-1", TipoMovimentoFinanceiro.Entrada, Money.DeReais(300), DateTimeOffset.UtcNow, corrente: CorrenteDeReceita.Comercio);

        await repo.SalvarAsync(movimento);
        var lido = await repo.ObterPorIdAsync(movimento.Id);

        Assert.NotNull(lido);
        Assert.Equal(movimento.Id, lido!.Id);
        Assert.Equal(movimento.BusinessId, lido.BusinessId);
        Assert.Equal(movimento.ContaBancariaCaixaId, lido.ContaBancariaCaixaId);
        Assert.Equal(movimento.FormaPagamentoId, lido.FormaPagamentoId);
        Assert.Equal(movimento.ParcelaId, lido.ParcelaId);
        Assert.Equal(movimento.ContaOrigemId, lido.ContaOrigemId);
        Assert.Equal(movimento.Tipo, lido.Tipo);
        Assert.Equal(movimento.Valor, lido.Valor);
        Assert.Equal(movimento.DataMovimento, lido.DataMovimento);
        Assert.Equal(movimento.Origem, lido.Origem);
        Assert.Equal(movimento.ReversalOfId, lido.ReversalOfId);
        Assert.Equal(movimento.CriadoEm, lido.CriadoEm);
        Assert.Equal(movimento.Corrente, lido.Corrente);
    }

    /// <summary>P0-1 (docs/financeiro/revisao-domain-fit-cnpj.md) — movimento sem corrente
    /// informada (o caso legado antes desta dimensão existir) continua vindo <c>null</c>.</summary>
    [Fact]
    public async Task Salvar_sem_corrente_persiste_null()
    {
        var repo = CriarRepositorio();
        var movimento = CriarMovimento(BusinessA, "origem-sem-corrente", TipoMovimentoFinanceiro.Entrada, Money.DeReais(300), DateTimeOffset.UtcNow);

        await repo.SalvarAsync(movimento);

        Assert.Null((await repo.ObterPorIdAsync(movimento.Id))!.Corrente);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync("movimento-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_origem_retorna_o_movimento()
    {
        var repo = CriarRepositorio();
        var movimento = CriarMovimento(BusinessA, "origem-2", TipoMovimentoFinanceiro.Entrada, Money.DeReais(150), DateTimeOffset.UtcNow);
        await repo.SalvarAsync(movimento);

        var lido = await repo.BuscarPorOrigemAsync(BusinessA, movimento.Origem.Chave);

        Assert.NotNull(lido);
        Assert.Equal(movimento.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_origem_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var movimento = CriarMovimento(BusinessA, "origem-3", TipoMovimentoFinanceiro.Entrada, Money.DeReais(150), DateTimeOffset.UtcNow);
        await repo.SalvarAsync(movimento);

        Assert.Null(await repo.BuscarPorOrigemAsync(BusinessB, movimento.Origem.Chave));
    }

    [Fact]
    public async Task Salvar_o_estorno_e_buscar_por_movimento_original_retorna_o_estorno()
    {
        var repo = CriarRepositorio();
        var original = CriarMovimento(BusinessA, "origem-4", TipoMovimentoFinanceiro.Entrada, Money.DeReais(500), DateTimeOffset.UtcNow);
        await repo.SalvarAsync(original);

        var estorno = original.GerarEstorno(DateTimeOffset.UtcNow.AddDays(1), new SourceRef("vendas", "origem-4-estorno")).Valor;
        await repo.SalvarAsync(estorno);

        var lido = await repo.BuscarEstornoDeAsync(original.Id);

        Assert.NotNull(lido);
        Assert.Equal(estorno.Id, lido!.Id);
        Assert.Equal(TipoMovimentoFinanceiro.Saida, lido.Tipo);
    }

    [Fact]
    public async Task ListarPorPeriodoAsync_retorna_apenas_movimentos_no_intervalo()
    {
        var repo = CriarRepositorio();
        var dentro = CriarMovimento(BusinessA, "origem-5", TipoMovimentoFinanceiro.Entrada, Money.DeReais(10), new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var fora = CriarMovimento(BusinessA, "origem-6", TipoMovimentoFinanceiro.Entrada, Money.DeReais(10), new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.SalvarAsync(dentro);
        await repo.SalvarAsync(fora);

        var lista = await repo.ListarPorPeriodoAsync(
            BusinessA, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(lista);
        Assert.Equal(dentro.Id, lista[0].Id);
    }

    [Fact]
    public async Task CalcularSaldoAsync_soma_entradas_e_subtrai_saidas_ate_a_data()
    {
        var repo = CriarRepositorio();
        var referencia = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        var entrada = CriarMovimento(BusinessA, "origem-7", TipoMovimentoFinanceiro.Entrada, Money.DeReais(300), referencia.AddDays(-1));
        var saida = CriarMovimento(BusinessA, "origem-8", TipoMovimentoFinanceiro.Saida, Money.DeReais(100), referencia.AddDays(-1));
        var depoisDaData = CriarMovimento(BusinessA, "origem-9", TipoMovimentoFinanceiro.Entrada, Money.DeReais(1000), referencia.AddDays(1));
        await repo.SalvarAsync(entrada);
        await repo.SalvarAsync(saida);
        await repo.SalvarAsync(depoisDaData);

        var saldo = await repo.CalcularSaldoAsync(BusinessA, null, referencia);

        Assert.Equal(Money.DeReais(200), saldo);
    }

    [Fact]
    public async Task CalcularSaldoAsync_filtra_por_conta_bancaria_caixa_quando_informada()
    {
        var repo = CriarRepositorio();
        var referencia = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);

        var daContaA = CriarMovimento(BusinessA, "origem-10", TipoMovimentoFinanceiro.Entrada, Money.DeReais(300), referencia.AddDays(-1), "caixa-a");
        var daContaB = CriarMovimento(BusinessA, "origem-11", TipoMovimentoFinanceiro.Entrada, Money.DeReais(1000), referencia.AddDays(-1), "caixa-b");
        await repo.SalvarAsync(daContaA);
        await repo.SalvarAsync(daContaB);

        var saldo = await repo.CalcularSaldoAsync(BusinessA, "caixa-a", referencia);

        Assert.Equal(Money.DeReais(300), saldo);
    }

    [Fact]
    public async Task ListarPorContaOrigemAsync_retorna_apenas_movimentos_da_conta_pedida()
    {
        var repo = CriarRepositorio();
        var daContaX = CriarMovimento(BusinessA, "origem-12", TipoMovimentoFinanceiro.Saida, Money.DeReais(120), DateTimeOffset.UtcNow, contaOrigemId: "conta-x");
        var outraDaContaX = CriarMovimento(BusinessA, "origem-13", TipoMovimentoFinanceiro.Entrada, Money.DeReais(10), DateTimeOffset.UtcNow, contaOrigemId: "conta-x");
        var daContaY = CriarMovimento(BusinessA, "origem-14", TipoMovimentoFinanceiro.Saida, Money.DeReais(999), DateTimeOffset.UtcNow, contaOrigemId: "conta-y");
        await repo.SalvarAsync(daContaX);
        await repo.SalvarAsync(outraDaContaX);
        await repo.SalvarAsync(daContaY);

        var lista = await repo.ListarPorContaOrigemAsync(BusinessA, "conta-x");

        Assert.Equal(2, lista.Count);
        Assert.All(lista, m => Assert.Equal("conta-x", m.ContaOrigemId));
    }

    [Fact]
    public async Task ListarPorContaOrigemAsync_isola_por_business()
    {
        var repo = CriarRepositorio();
        var deA = CriarMovimento(BusinessA, "origem-15", TipoMovimentoFinanceiro.Saida, Money.DeReais(120), DateTimeOffset.UtcNow, contaOrigemId: "conta-compartilhada");
        var deB = CriarMovimento(BusinessB, "origem-16", TipoMovimentoFinanceiro.Saida, Money.DeReais(50), DateTimeOffset.UtcNow, contaOrigemId: "conta-compartilhada");
        await repo.SalvarAsync(deA);
        await repo.SalvarAsync(deB);

        var lista = await repo.ListarPorContaOrigemAsync(BusinessA, "conta-compartilhada");

        Assert.Single(lista);
        Assert.Equal(deA.Id, lista[0].Id);
    }
}
