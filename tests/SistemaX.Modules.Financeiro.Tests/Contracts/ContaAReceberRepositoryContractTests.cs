using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IContaAReceberRepository"/> — roda o mesmo conjunto de casos
/// contra QUALQUER adapter (<c>InMemoryContaAReceberRepository</c> e
/// <c>SqliteContaAReceberRepository</c>). Molde: <c>FornecedorRepositoryContractTests</c>
/// (F0, Compras) — ver docs/persistencia/persistencia-sqlite.md.
/// </summary>
public abstract class ContaAReceberRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IContaAReceberRepository CriarRepositorio();

    private static ContaAReceber CriarConta(
        string businessId, string origemId, Money valor, DateTimeOffset dataCompetencia, DateTimeOffset vencimento,
        string? clienteId = null, CorrenteDeReceita? corrente = null, string? tecnicoId = null,
        Money? valorServico = null, Money? valorPecas = null)
    {
        var sourceRef = new SourceRef("vendas", origemId);
        var parcelas = ContaFinanceiraBase.ParcelaUnica(valor, vencimento);
        return ContaAReceber.Criar(
            businessId, sourceRef, "Conta de teste", "servicos", dataCompetencia, valor, parcelas, clienteId: clienteId, corrente: corrente,
            tecnicoId: tecnicoId, valorServico: valorServico, valorPecas: valorPecas).Valor;
    }

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_conta_com_parcelas()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "origem-1", Money.DeReais(100), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), clienteId: "cliente-1");

        await repo.SalvarAsync(conta);
        var lido = await repo.ObterPorIdAsync(conta.Id);

        Assert.NotNull(lido);
        Assert.Equal(conta.Id, lido!.Id);
        Assert.Equal(conta.BusinessId, lido.BusinessId);
        Assert.Equal(conta.SourceRef, lido.SourceRef);
        Assert.Equal(conta.Descricao, lido.Descricao);
        Assert.Equal(conta.CategoriaId, lido.CategoriaId);
        Assert.Equal(conta.CentroDeCustoId, lido.CentroDeCustoId);
        Assert.Equal(conta.DataCompetencia, lido.DataCompetencia);
        Assert.Equal(conta.ValorTotal, lido.ValorTotal);
        Assert.Equal(conta.Status, lido.Status);
        Assert.Equal(conta.CriadoEm, lido.CriadoEm);
        Assert.Equal(conta.ClienteId, lido.ClienteId);
        Assert.Equal(conta.Corrente, lido.Corrente);

        Assert.Single(lido.Parcelas);
        var parcelaOriginal = conta.Parcelas[0];
        var parcelaLida = lido.Parcelas[0];
        Assert.Equal(parcelaOriginal.Id, parcelaLida.Id);
        Assert.Equal(parcelaOriginal.Numero, parcelaLida.Numero);
        Assert.Equal(parcelaOriginal.Vencimento, parcelaLida.Vencimento);
        Assert.Equal(parcelaOriginal.Valor, parcelaLida.Valor);
        Assert.Equal(parcelaOriginal.ValorPago, parcelaLida.ValorPago);
        Assert.Equal(parcelaOriginal.Status, parcelaLida.Status);
        Assert.Equal(parcelaOriginal.DataLiquidacao, parcelaLida.DataLiquidacao);
        Assert.Equal(parcelaOriginal.FormaPagamentoId, parcelaLida.FormaPagamentoId);
    }

    /// <summary>P0-1 (docs/financeiro/revisao-domain-fit-cnpj.md) — a corrente sobrevive ao
    /// roundtrip de persistência; uma conta sem corrente informada (o caso legado antes desta
    /// dimensão existir) continua vindo <c>null</c>, não um valor arbitrário.</summary>
    [Fact]
    public async Task Salvar_e_buscar_persiste_a_corrente_quando_informada()
    {
        var repo = CriarRepositorio();
        var comCorrente = CriarConta(BusinessA, "origem-corrente-1", Money.DeReais(50), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), corrente: CorrenteDeReceita.Servico);
        var semCorrente = CriarConta(BusinessA, "origem-corrente-2", Money.DeReais(50), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10));

        await repo.SalvarAsync(comCorrente);
        await repo.SalvarAsync(semCorrente);

        Assert.Equal(CorrenteDeReceita.Servico, (await repo.ObterPorIdAsync(comCorrente.Id))!.Corrente);
        Assert.Null((await repo.ObterPorIdAsync(semCorrente.Id))!.Corrente);
    }

    /// <summary>P1-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — técnico e repartição
    /// mão de obra/peças sobrevivem ao roundtrip; uma conta sem essas dimensões (a maioria das
    /// origens — venda, pedido, assinatura) continua vindo <c>null</c>, não um valor arbitrário.</summary>
    [Fact]
    public async Task Salvar_e_buscar_persiste_tecnico_e_reparticao_servico_pecas_quando_informados()
    {
        var repo = CriarRepositorio();
        var comTecnicoERepartição = CriarConta(
            BusinessA, "origem-tecnico-1", Money.DeReais(150), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1),
            tecnicoId: "tecnico-1", valorServico: Money.DeReais(100), valorPecas: Money.DeReais(50));
        var semTecnicoNemRepartição = CriarConta(
            BusinessA, "origem-tecnico-2", Money.DeReais(150), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        await repo.SalvarAsync(comTecnicoERepartição);
        await repo.SalvarAsync(semTecnicoNemRepartição);

        var lidaComDados = await repo.ObterPorIdAsync(comTecnicoERepartição.Id);
        Assert.Equal("tecnico-1", lidaComDados!.TecnicoId);
        Assert.Equal(Money.DeReais(100), lidaComDados.ValorServico);
        Assert.Equal(Money.DeReais(50), lidaComDados.ValorPecas);

        var lidaSemDados = await repo.ObterPorIdAsync(semTecnicoNemRepartição.Id);
        Assert.Null(lidaSemDados!.TecnicoId);
        Assert.Null(lidaSemDados.ValorServico);
        Assert.Null(lidaSemDados.ValorPecas);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync("conta-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_origem_retorna_a_conta()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "origem-2", Money.DeReais(200), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(15));
        await repo.SalvarAsync(conta);

        var lido = await repo.BuscarPorOrigemAsync(BusinessA, conta.SourceRef.Chave);

        Assert.NotNull(lido);
        Assert.Equal(conta.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_origem_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "origem-3", Money.DeReais(300), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10));
        await repo.SalvarAsync(conta);

        Assert.Null(await repo.BuscarPorOrigemAsync(BusinessB, conta.SourceRef.Chave));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_liquidacao_de_parcela_reflete_novo_estado()
    {
        var repo = CriarRepositorio();
        var conta = CriarConta(BusinessA, "origem-4", Money.DeReais(100), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(5));
        await repo.SalvarAsync(conta);

        conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(100), DateTimeOffset.UtcNow, "pix");
        await repo.SalvarAsync(conta);

        var lido = await repo.ObterPorIdAsync(conta.Id);
        Assert.Equal(StatusFinanceiro.Pago, lido!.Status);
        Assert.Equal(StatusFinanceiro.Pago, lido.Parcelas[0].Status);
        Assert.Equal(Money.DeReais(100), lido.Parcelas[0].ValorPago);
        Assert.NotNull(lido.Parcelas[0].DataLiquidacao);
        Assert.Equal("pix", lido.Parcelas[0].FormaPagamentoId);
    }

    [Fact]
    public async Task ListarPorCompetenciaAsync_retorna_apenas_contas_no_intervalo()
    {
        var repo = CriarRepositorio();
        var dentro = CriarConta(BusinessA, "origem-5", Money.DeReais(10), new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow.AddDays(30));
        var fora = CriarConta(BusinessA, "origem-6", Money.DeReais(10), new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow.AddDays(30));
        await repo.SalvarAsync(dentro);
        await repo.SalvarAsync(fora);

        var lista = await repo.ListarPorCompetenciaAsync(
            BusinessA, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(lista);
        Assert.Equal(dentro.Id, lista[0].Id);
    }

    [Fact]
    public async Task ListarAbertasAteAsync_retorna_contas_com_parcela_vencendo_ate_a_referencia()
    {
        var repo = CriarRepositorio();
        var referencia = DateTimeOffset.UtcNow.AddDays(10);

        var vencendoDentro = CriarConta(BusinessA, "origem-7", Money.DeReais(10), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(5));
        var vencendoDepois = CriarConta(BusinessA, "origem-8", Money.DeReais(10), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(60));
        await repo.SalvarAsync(vencendoDentro);
        await repo.SalvarAsync(vencendoDepois);

        var lista = await repo.ListarAbertasAteAsync(BusinessA, referencia);

        Assert.Contains(lista, c => c.Id == vencendoDentro.Id);
        Assert.DoesNotContain(lista, c => c.Id == vencendoDepois.Id);
    }
}
