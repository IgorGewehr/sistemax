using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="ISessaoCaixaRepository"/> — roda o MESMO conjunto de
/// casos contra <c>InMemorySessaoCaixaRepository</c> e <c>SqliteSessaoCaixaRepository</c>.</summary>
public abstract class SessaoCaixaRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";
    protected const string ContaCaixa = "conta-caixa-padrao";

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 9, 0, 0, TimeSpan.Zero);

    protected abstract ISessaoCaixaRepository CriarRepositorio();

    private static SessaoCaixa AbrirSessao(string businessId, string contaCaixaId, DateTimeOffset abertaEm, long aberturaCentavos = 20_000)
        => SessaoCaixa.Abrir(businessId, contaCaixaId, "op-1", "Ana", new Money(aberturaCentavos), abertaEm).Valor;

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_sessao_sem_movimentos()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);

        await repo.SalvarAsync(sessao);
        var lida = await repo.ObterPorIdAsync(BusinessA, sessao.Id);

        Assert.NotNull(lida);
        Assert.Equal(sessao.Id, lida!.Id);
        Assert.Equal(sessao.BusinessId, lida.BusinessId);
        Assert.Equal(sessao.ContaCaixaId, lida.ContaCaixaId);
        Assert.Equal(sessao.OperadorId, lida.OperadorId);
        Assert.Equal(sessao.OperadorNome, lida.OperadorNome);
        Assert.Equal(sessao.SaldoAbertura, lida.SaldoAbertura);
        Assert.Equal(StatusSessaoCaixa.Aberta, lida.Status);
        Assert.Empty(lida.Movimentos);
        Assert.Null(lida.FechadaEm);
        Assert.Null(lida.SaldoInformado);
    }

    [Fact]
    public async Task Salvar_persiste_os_movimentos_e_reidrata_na_mesma_ordem()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);
        sessao.RegistrarSuprimento(new Money(5_000), "reforço de troco", Agora.AddMinutes(10), "op-1", "Ana");
        sessao.RegistrarVendaEmEspecie(new Money(3_000), Agora.AddMinutes(20), "op-1", "Ana");
        sessao.RegistrarSangria(new Money(2_000), "depósito Itaú PJ", Agora.AddMinutes(30), "op-1", "Ana");

        await repo.SalvarAsync(sessao);
        var lida = await repo.ObterPorIdAsync(BusinessA, sessao.Id);

        Assert.NotNull(lida);
        Assert.Equal(3, lida!.Movimentos.Count);
        Assert.Equal(TipoMovimentoCaixa.Suprimento, lida.Movimentos[0].Tipo);
        Assert.Equal(TipoMovimentoCaixa.VendaEmEspecie, lida.Movimentos[1].Tipo);
        Assert.Equal(TipoMovimentoCaixa.Sangria, lida.Movimentos[2].Tipo);
        Assert.Equal("depósito Itaú PJ", lida.Movimentos[2].Motivo);
        Assert.Equal(sessao.SaldoEsperado, lida.SaldoEsperado);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_fechar_reflete_status_contagem_e_diferenca()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora, 20_000);
        await repo.SalvarAsync(sessao);

        sessao.Fechar(new Money(19_500), Agora.AddHours(8));
        await repo.SalvarAsync(sessao);

        var lida = await repo.ObterPorIdAsync(BusinessA, sessao.Id);
        Assert.Equal(StatusSessaoCaixa.Fechada, lida!.Status);
        Assert.Equal(new Money(19_500), lida.SaldoInformado);
        Assert.Equal(new Money(-500), lida.Diferenca);
        Assert.NotNull(lida.FechadaEm);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, "sessao-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_de_outro_business_nao_retorna_R1()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);
        await repo.SalvarAsync(sessao);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, sessao.Id));
    }

    [Fact]
    public async Task ObterAbertaPorContaAsync_retorna_a_sessao_aberta_da_conta()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);
        await repo.SalvarAsync(sessao);

        var aberta = await repo.ObterAbertaPorContaAsync(BusinessA, ContaCaixa);

        Assert.NotNull(aberta);
        Assert.Equal(sessao.Id, aberta!.Id);
    }

    [Fact]
    public async Task ObterAbertaPorContaAsync_retorna_null_quando_a_sessao_ja_fechou()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);
        sessao.Fechar(new Money(20_000), Agora.AddHours(8));
        await repo.SalvarAsync(sessao);

        Assert.Null(await repo.ObterAbertaPorContaAsync(BusinessA, ContaCaixa));
    }

    [Fact]
    public async Task ObterAbertaPorContaAsync_nao_cruza_business_R1()
    {
        var repo = CriarRepositorio();
        var sessao = AbrirSessao(BusinessA, ContaCaixa, Agora);
        await repo.SalvarAsync(sessao);

        Assert.Null(await repo.ObterAbertaPorContaAsync(BusinessB, ContaCaixa));
    }

    [Fact]
    public async Task ListarAsync_retorna_apenas_da_conta_e_do_business_pedidos_mais_recente_primeiro()
    {
        var repo = CriarRepositorio();
        var maisAntiga = AbrirSessao(BusinessA, ContaCaixa, Agora);
        maisAntiga.Fechar(new Money(20_000), Agora.AddHours(8));
        await repo.SalvarAsync(maisAntiga);

        var maisRecente = AbrirSessao(BusinessA, ContaCaixa, Agora.AddDays(1));
        await repo.SalvarAsync(maisRecente);

        var deOutraConta = AbrirSessao(BusinessA, "outra-conta", Agora.AddDays(2));
        await repo.SalvarAsync(deOutraConta);

        var deOutroBusiness = AbrirSessao(BusinessB, ContaCaixa, Agora.AddDays(3));
        await repo.SalvarAsync(deOutroBusiness);

        var lista = await repo.ListarAsync(BusinessA, ContaCaixa);

        Assert.Equal(2, lista.Count);
        Assert.Equal(maisRecente.Id, lista[0].Id);
        Assert.Equal(maisAntiga.Id, lista[1].Id);
    }

    [Fact]
    public async Task ListarAsync_filtra_por_periodo_de_abertura()
    {
        var repo = CriarRepositorio();
        var dia1 = AbrirSessao(BusinessA, ContaCaixa, new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero));
        var dia10 = AbrirSessao(BusinessA, ContaCaixa, new DateTimeOffset(2026, 7, 10, 8, 0, 0, TimeSpan.Zero));
        var dia20 = AbrirSessao(BusinessA, ContaCaixa, new DateTimeOffset(2026, 7, 20, 8, 0, 0, TimeSpan.Zero));
        await repo.SalvarAsync(dia1);
        await repo.SalvarAsync(dia10);
        await repo.SalvarAsync(dia20);

        var lista = await repo.ListarAsync(
            BusinessA, ContaCaixa,
            de: new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero),
            ate: new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(lista);
        Assert.Equal(dia10.Id, lista[0].Id);
    }
}
