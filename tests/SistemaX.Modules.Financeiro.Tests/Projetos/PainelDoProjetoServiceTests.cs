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
        FakeRelogio Relogio) NovoAmbiente(DateTimeOffset agora)
        => (new InMemoryProjetoRepository(), new InMemoryAssinaturaRepository(), new InMemoryContaAReceberRepository(),
            new InMemoryContaAPagarRepository(), new FakeRelogio(agora));

    [Fact]
    public async Task DigiSat_1Assinatura280_SemCancelamento_MrrChurnLtvEMc1CalculamCorretos()
    {
        var criacaoProjeto = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio) = NovoAmbiente(agora);
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

        var servico = new PainelDoProjetoService(projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio);
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
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio) = NovoAmbiente(agora);

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

        var servico = new PainelDoProjetoService(projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio);
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
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio) = NovoAmbiente(agora);

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

        var servico = new PainelDoProjetoService(projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio);
        var resultado = (await servico.CalcularAsync(Biz, projeto.Id)).Valor;

        Assert.Equal(Money.DeReais(1000), resultado.Margem.Receita);
        Assert.Equal(Money.DeReais(150), resultado.Margem.CustoDireto);
        Assert.Equal(Money.DeReais(850), resultado.Margem.Mc1);
    }

    [Fact]
    public async Task ProjetoInexistente_Retorna404Semantico()
    {
        var (projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio) = NovoAmbiente(new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero));
        var servico = new PainelDoProjetoService(projetosRepo, assinaturasRepo, contasAReceber, contasAPagar, relogio);

        var resultado = await servico.CalcularAsync(Biz, "projeto-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projeto.nao_encontrado", resultado.Erro.Codigo);
    }
}
