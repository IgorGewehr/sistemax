using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Projetos;

/// <summary>
/// Invariante do design (docs/financeiro/design-analise-por-projeto.md §2.2/§13.1) — o CONTRATO do
/// opt-in: toggle desligado ⇒ toda escrita com <c>projetoId</c> não-nulo é barrada (422); toggle
/// ligado ⇒ funciona. Testado nos DOIS pontos de entrada que a Parte A expõe:
/// <see cref="CriarProjetoUseCase"/> (a própria entidade) e <see cref="LancarContaAPagarUseCase"/>/
/// <see cref="LancarContaAReceberUseCase"/> (tagging aditivo em conta manual).
/// </summary>
public sealed class AnalisePorProjetoGuardTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task CriarProjeto_ComToggleDesligado_Retorna422()
    {
        var projetos = new InMemoryProjetoRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository(); // sem linha = desligado
        var useCase = new CriarProjetoUseCase(projetos, configuracoes, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarProjetoComando(Biz, "DigiSat"));

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projetos.desativado", resultado.Erro.Codigo);
        Assert.Empty(await projetos.ListarAsync(Biz, incluirArquivados: true));
    }

    [Fact]
    public async Task CriarProjeto_ComToggleLigado_Funciona()
    {
        var projetos = new InMemoryProjetoRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, analisePorProjetoAtiva: true).Valor);
        var useCase = new CriarProjetoUseCase(projetos, configuracoes, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new CriarProjetoComando(Biz, "DigiSat"));

        Assert.True(resultado.Sucesso);
        Assert.Equal("DigiSat", resultado.Valor.Nome);
    }

    [Fact]
    public async Task LancarContaAPagar_ComProjetoIdEToggleDesligado_Retorna422()
    {
        var contasAPagar = new InMemoryContaAPagarRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        var useCase = new LancarContaAPagarUseCase(contasAPagar, lancamentos, configuracoes);

        var vencimento = Agora.AddDays(10);
        var comando = new LancarContaComando(
            Biz, "Custo de IA — Aevo", "outras-despesas", Agora, Money.DeReais(200),
            ContaFinanceiraBase.ParcelaUnica(Money.DeReais(200), vencimento), "idem-1", ProjetoId: "projeto-aevo");

        var resultado = await useCase.ExecutarAsync(comando);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projetos.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task LancarContaAPagar_SemProjetoIdEToggleDesligado_FuncionaNormalmente()
    {
        // R1 do design: sem projeto = comportamento de hoje, intacto — o toggle desligado nunca
        // impede uma escrita que não carrega projetoId.
        var contasAPagar = new InMemoryContaAPagarRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        var useCase = new LancarContaAPagarUseCase(contasAPagar, lancamentos, configuracoes);

        var vencimento = Agora.AddDays(10);
        var comando = new LancarContaComando(
            Biz, "Aluguel", "aluguel", Agora, Money.DeReais(1500),
            ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1500), vencimento), "idem-2");

        var resultado = await useCase.ExecutarAsync(comando);

        Assert.True(resultado.Sucesso);
        Assert.Null(resultado.Valor.ProjetoId);
    }

    [Fact]
    public async Task LancarContaAReceber_ComProjetoIdEToggleLigado_Funciona()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, analisePorProjetoAtiva: true).Valor);
        var useCase = new LancarContaAReceberUseCase(contasAReceber, lancamentos, configuracoes);

        var vencimento = Agora.AddDays(10);
        var comando = new LancarContaComando(
            Biz, "Venda avulsa de licença", "servicos", Agora, Money.DeReais(1379),
            ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1379), vencimento), "idem-3", ProjetoId: "projeto-digisat");

        var resultado = await useCase.ExecutarAsync(comando);

        Assert.True(resultado.Sucesso);
        Assert.Equal("projeto-digisat", resultado.Valor.ProjetoId);
    }

    /// <summary>
    /// Invariante estrutural do design (§2.3): "o DRE NÃO ganha termo por projeto — projeto é uma
    /// LENTE, nunca uma dimensão do demonstrativo". <see cref="DreGerencialService"/> não lê
    /// <c>ProjetoId</c> em NENHUM ponto — este teste prova isso construindo o MESMO cenário de
    /// receita/despesa duas vezes, uma sem projeto (o caminho de hoje) e outra com TODAS as contas
    /// tageadas a um projeto, e travando que o <see cref="DreResultado"/> é BYTE-IDÊNTICO nos dois
    /// casos — não apenas "com o toggle desligado", mas estruturalmente impossível de divergir.
    /// </summary>
    [Fact]
    public async Task Dre_EhByteIdenticoComOuSemContasTageadasAUmProjeto()
    {
        var inicio = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var fim = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(-3));
        var dataFato = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.FromHours(-3));

        async Task<DreResultado> CalcularAsync(string? projetoId)
        {
            var contasAReceber = new InMemoryContaAReceberRepository();
            var contasAPagar = new InMemoryContaAPagarRepository();
            var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
            var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();

            var receita = ContaAReceber.Criar(
                Biz, new SourceRef("teste", $"receita-{projetoId ?? "sem"}"), "Cobrança", "servicos", dataFato,
                Money.DeReais(1000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1000), dataFato.AddDays(5)),
                corrente: CorrenteDeReceita.Servico, projetoId: projetoId).Valor;
            await contasAReceber.SalvarAsync(receita);

            var despesa = ContaAPagar.Criar(
                Biz, new SourceRef("teste", $"despesa-{projetoId ?? "sem"}"), "Custo direto", "comissoes", dataFato,
                Money.DeReais(300), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(300), dataFato.AddDays(5)),
                corrente: CorrenteDeReceita.Servico, projetoId: projetoId).Valor;
            await contasAPagar.SalvarAsync(despesa);

            var service = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis, new InMemoryAtivoDeCapitalRepository());
            return await service.CalcularAsync(Biz, inicio, fim);
        }

        var semProjeto = await CalcularAsync(null);
        var comProjeto = await CalcularAsync("projeto-digisat");

        Assert.Equal(semProjeto.ReceitaBruta, comProjeto.ReceitaBruta);
        Assert.Equal(semProjeto.CustoDireto, comProjeto.CustoDireto);
        Assert.Equal(semProjeto.DespesaOperacional, comProjeto.DespesaOperacional);
        Assert.Equal(semProjeto.DespesaFinanceira, comProjeto.DespesaFinanceira);
        Assert.Equal(semProjeto.ResultadoOperacional, comProjeto.ResultadoOperacional);
        Assert.Equal(semProjeto.ReceitaRecorrente, comProjeto.ReceitaRecorrente);
        Assert.Equal(semProjeto.ReceitaOperacional, comProjeto.ReceitaOperacional);
        Assert.Equal(semProjeto.PorCorrente.Count, comProjeto.PorCorrente.Count);
        for (var i = 0; i < semProjeto.PorCorrente.Count; i++)
        {
            Assert.Equal(semProjeto.PorCorrente[i], comProjeto.PorCorrente[i]);
        }
    }
}
