using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Projetos;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Projetos;

/// <summary>
/// Painel do Projeto v1 (docs/financeiro/design-analise-por-projeto.md §9, Parte A) — o cenário
/// nominal DigiSat citado no design (§9.5, adaptado para o subconjunto de métricas da Parte A: sem
/// ativo amortizável, então MC1 aqui é a margem cheia porque não há despesa direta tageada):
/// 1 assinatura de R$280/mês, zero cancelamentos observados.
/// </summary>
public sealed class PainelDoProjetoServiceTests
{
    private const string Biz = "loja-1";

    private static (
        InMemoryProjetoRepository Projetos, InMemoryAssinaturaRepository Assinaturas,
        InMemoryContaAReceberRepository ContasAReceber, InMemoryContaAPagarRepository ContasAPagar,
        InMemoryAtivoDeCapitalRepository AtivosDeCapital, InMemoryMovimentoFinanceiroRepository Movimentos,
        InMemoryApontamentoDeTempoRepository Apontamentos, FakeRelogio Relogio) NovoAmbiente(DateTimeOffset agora)
        => (new InMemoryProjetoRepository(), new InMemoryAssinaturaRepository(), new InMemoryContaAReceberRepository(),
            new InMemoryContaAPagarRepository(), new InMemoryAtivoDeCapitalRepository(), new InMemoryMovimentoFinanceiroRepository(),
            new InMemoryApontamentoDeTempoRepository(), new FakeRelogio(agora));

    private static PainelDoProjetoService NovoServico((
        InMemoryProjetoRepository Projetos, InMemoryAssinaturaRepository Assinaturas,
        InMemoryContaAReceberRepository ContasAReceber, InMemoryContaAPagarRepository ContasAPagar,
        InMemoryAtivoDeCapitalRepository AtivosDeCapital, InMemoryMovimentoFinanceiroRepository Movimentos,
        InMemoryApontamentoDeTempoRepository Apontamentos, FakeRelogio Relogio) ambiente)
        => new(ambiente.Projetos, ambiente.Assinaturas, ambiente.ContasAReceber, ambiente.ContasAPagar,
            ambiente.AtivosDeCapital, ambiente.Movimentos, ambiente.Apontamentos, ambiente.Relogio);

    [Fact]
    public async Task DigiSat_1Assinatura280_SemCancelamento_MrrChurnLtvEMc1CalculamCorretos()
    {
        var criacaoProjeto = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));
        var ambiente = NovoAmbiente(agora);
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar) = (ambiente.Projetos, ambiente.Assinaturas, ambiente.ContasAReceber, ambiente.ContasAPagar);
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var projeto = Projeto.Criar(Biz, "DigiSat", "Revenda de licenças", criacaoProjeto).Valor;
        await projetosRepo.SalvarAsync(projeto);

        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-digisat", "Licença DigiSat", Money.DeReais(280),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: projeto.Id).Valor;
        await assinaturasRepo.SalvarAsync(assinatura);

        // Faturamento real do mês corrente (mesmo motor de produção) — garante que a ContaAReceber
        // do mês existe e carrega o projeto (Assinatura.GerarCobranca propaga).
        var gerarCobrancas = new GerarCobrancasAssinaturasUseCase(assinaturasRepo, contasAReceber, lancamentos, new FakeIntegrationEventBus());
        await gerarCobrancas.ExecutarAsync(Biz, agora);

        var servico = NovoServico(ambiente);
        var resultado = (await servico.CalcularAsync(Biz, projeto.Id)).Valor;

        // Receita/MRR
        Assert.Equal(Money.DeReais(280), resultado.Receita.Mrr);
        Assert.Equal(Money.DeReais(280) * 12, resultado.Receita.Arr);
        Assert.Equal(1, resultado.Receita.AssinaturasAtivas);
        Assert.Equal(Money.DeReais(280), resultado.Receita.TicketMedio);

        // Churn — zero cancelamentos observados ⇒ λ=0.
        Assert.Equal(0, resultado.Churn.Cancelamentos12m);
        Assert.Equal(0m, resultado.Churn.ChurnMensalPercent);
        Assert.Null(resultado.Churn.VidaEsperadaMeses);

        // LTV — null honesto (design §9.4): "churn=0 na janela — LTV indefinido".
        Assert.Null(resultado.Ltv.Ltv);
        Assert.NotNull(resultado.Ltv.Observacao);
        Assert.Contains("indefinido", resultado.Ltv.Observacao!);
        // Piso realizado > 0 — a margem acumulada desde a criação do projeto já é positiva.
        Assert.True(resultado.Ltv.LimiteInferior.Centavos > 0);

        // MC1 do mês — sem despesa direta tageada, margem = receita cheia (R$280).
        Assert.Equal(Money.DeReais(280), resultado.Margem.Receita);
        Assert.Equal(Money.Zero, resultado.Margem.CustoDireto);
        Assert.Equal(Money.DeReais(280), resultado.Margem.Mc1);
        Assert.Equal(100m, resultado.Margem.Mc1Percent);
    }

    [Fact]
    public async Task ComCancelamentoNaJanela_ChurnELtvSaoDefinidos()
    {
        var criacaoProjeto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));
        var ambiente = NovoAmbiente(agora);
        var (projetosRepo, assinaturasRepo) = (ambiente.Projetos, ambiente.Assinaturas);

        var projeto = Projeto.Criar(Biz, "Aevo", null, criacaoProjeto).Valor;
        await projetosRepo.SalvarAsync(projeto);

        // Assinatura ativa (permanece exposta o W inteiro).
        var ativa = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-aevo", "Aevo Pro", Money.DeReais(500),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: projeto.Id).Valor;
        await assinaturasRepo.SalvarAsync(ativa);

        // Assinatura cancelada 2 meses atrás — dentro da janela de 12 meses.
        var cancelada = Assinatura.Criar(
            Biz, "cliente-2", "Empresa Y", "servico-aevo", "Aevo Pro", Money.DeReais(500),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: projeto.Id).Valor;
        cancelada.Cancelar("Cliente pediu cancelamento", agora.AddMonths(-2));
        await assinaturasRepo.SalvarAsync(cancelada);

        var servico = NovoServico(ambiente);
        var resultado = (await servico.CalcularAsync(Biz, projeto.Id)).Valor;

        Assert.Equal(1, resultado.Churn.Cancelamentos12m);
        Assert.True(resultado.Churn.ChurnMensalPercent > 0m);
        Assert.NotNull(resultado.Churn.VidaEsperadaMeses);
        Assert.NotNull(resultado.Ltv.Ltv);
        Assert.Null(resultado.Ltv.Observacao);
    }

    [Fact]
    public async Task Mc1SubtraiDespesaDiretaTageadaAoProjeto()
    {
        var criacaoProjeto = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));
        var ambiente = NovoAmbiente(agora);
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar) = (ambiente.Projetos, ambiente.Assinaturas, ambiente.ContasAReceber, ambiente.ContasAPagar);

        var projeto = Projeto.Criar(Biz, "Aevo", null, criacaoProjeto).Valor;
        await projetosRepo.SalvarAsync(projeto);

        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-aevo", "Aevo Pro", Money.DeReais(1000),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: projeto.Id).Valor;
        await assinaturasRepo.SalvarAsync(assinatura);

        var inicioMes = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var receita = ContaAReceber.Criar(
            Biz, new SourceRef("assinatura", $"assinatura-1:{inicioMes:yyyyMM}"), "Aevo Pro", "receita-recorrente", inicioMes,
            Money.DeReais(1000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1000), inicioMes.AddDays(5)),
            corrente: CorrenteDeReceita.Recorrente, projetoId: projeto.Id).Valor;
        await contasAReceber.SalvarAsync(receita);

        // Custo de IA do mês, tageado ao mesmo projeto (caso Aevo do design).
        var custoIa = ContaAPagar.Criar(
            Biz, new SourceRef("recorrencia", $"custo-ia:{inicioMes:yyyyMM}"), "Custo de IA", "outras-despesas", inicioMes,
            Money.DeReais(150), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(150), inicioMes.AddDays(5)),
            projetoId: projeto.Id).Valor;
        await contasAPagar.SalvarAsync(custoIa);

        var servico = NovoServico(ambiente);
        var resultado = (await servico.CalcularAsync(Biz, projeto.Id)).Valor;

        Assert.Equal(Money.DeReais(1000), resultado.Margem.Receita);
        Assert.Equal(Money.DeReais(150), resultado.Margem.CustoDireto);
        Assert.Equal(Money.DeReais(850), resultado.Margem.Mc1);
    }

    [Fact]
    public async Task ProjetoInexistente_Retorna404Semantico()
    {
        var ambiente = NovoAmbiente(new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero));
        var servico = NovoServico(ambiente);

        var resultado = await servico.CalcularAsync(Biz, "projeto-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projeto.nao_encontrado", resultado.Erro.Codigo);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // P3 — o cenário nominal DigiSat COMPLETO do design (§9.5/§9.6): MC2, capacidade/ociosidade e
    // payback projetado (a simulação determinística — Acum(0)=0 num tenant fresco reproduz
    // BYTE-A-BYTE a conta manual do design: meses 1–7 a −705/mês, cruza zero no mês 25).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DigiSat_ComAtivoDeCapitalE7ParcelasEmAberto_Mc2OciosidadeEPaybackBatemComODesign()
    {
        var criacaoProjeto = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));
        var ambiente = NovoAmbiente(agora);
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, ativosDeCapital) =
            (ambiente.Projetos, ambiente.Assinaturas, ambiente.ContasAReceber, ambiente.ContasAPagar, ambiente.AtivosDeCapital);
        var lancamentos = new InMemoryLancamentoContabilRepository();

        var projeto = Projeto.Criar(Biz, "DigiSat", "Revenda de licenças", criacaoProjeto).Valor;
        await projetosRepo.SalvarAsync(projeto);

        // 1 assinatura de R$280/mês (o caso do design) — capacidade de 5 licenças, 1 usada = 20%.
        // dataInicio em janeiro (como o teste v1 já fixado) garante que o catch-up de
        // GerarCobrancasAssinaturasUseCase já gerou a cobrança do mês CORRENTE (julho) até "agora".
        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-digisat", "Licença DigiSat", Money.DeReais(280),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.FromHours(-3)),
            projetoId: projeto.Id).Valor;
        await assinaturasRepo.SalvarAsync(assinatura);

        var gerarCobrancas = new GerarCobrancasAssinaturasUseCase(assinaturasRepo, contasAReceber, lancamentos, new FakeIntegrationEventBus());
        await gerarCobrancas.ExecutarAsync(Biz, agora);

        // O ATIVO: R$6.895, intangível, 36 meses, 5 unidades (design §4.3) — início em janeiro
        // (julho é o mês-índice 6, ainda dentro dos primeiros 28 meses a R$191,53 — Hamilton).
        var ativo = Domain.Ativos.AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", Domain.Ativos.NaturezaAtivo.Intangivel, Domain.Ativos.CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 36, criacaoProjeto,
            quantidadeUnidades: 5, projetoId: projeto.Id).Valor;
        await ativosDeCapital.SalvarAsync(ativo);

        // As 7 parcelas restantes do investimento (R$985 cada), em aberto, vencendo nos PRÓXIMOS
        // 7 meses (design §9.5: "meses 1–7 acumulam 280−985 = −705/mês").
        var parcelas = Enumerable.Range(1, 7)
            .Select(i => Parcela.Criar(i, agora.AddMonths(i), Money.DeReais(985)))
            .ToList();
        var contaInvestimento = ContaAPagar.Criar(
            Biz, new SourceRef("financeiro-ativo", ativo.Id), "Investimento — Licenças DigiSat",
            Application.Categorias.CategoriaFinanceiraPadrao.AtivoDeCapital, criacaoProjeto, Money.DeReais(6_895), parcelas, projetoId: projeto.Id).Valor;
        await contasAPagar.SalvarAsync(contaInvestimento);

        var servico = NovoServico(ambiente);
        var resultado = (await servico.CalcularAsync(Biz, projeto.Id)).Valor;

        // MC2 = 280 − 191,53 = R$88,47/mês (design §9.5).
        Assert.Equal(19_153, resultado.Margem.AmortizacaoMes.Centavos);
        Assert.Equal(8_847, resultado.Margem.Mc2.Centavos);

        // Capacidade/ociosidade (§9.6): 1/5 = 20% de utilização; ociosidade = 191,53 × 0,8 = R$153,22.
        Assert.Equal(5, resultado.Capacidade.UnidadesTotais);
        Assert.Equal(1, resultado.Capacidade.UnidadesUtilizadas);
        Assert.Equal(20.0m, resultado.Capacidade.UtilizacaoPercent);
        Assert.Equal(15_322, resultado.Capacidade.CustoOciosidadeMesCentavos);

        // Payback projetado: cruza zero no mês 25 (7 meses a −705 = −4.935; depois +280/mês).
        Assert.Equal(25, resultado.Payback.PaybackProjetadoMeses);
        Assert.Equal(689_500, resultado.Payback.InvestimentoTotalCentavos);
        Assert.Null(resultado.Payback.PaybackRealizadoEm); // nenhum MovimentoFinanceiro real ainda
    }
}
