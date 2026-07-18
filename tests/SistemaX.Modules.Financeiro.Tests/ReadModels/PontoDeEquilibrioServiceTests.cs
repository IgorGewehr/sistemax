using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// P1-2 (docs/financeiro/revisao-domain-fit-cnpj.md) — breakeven usa MC% BLENDED POR MIX (uma
/// margem por corrente, ponderada pela participação de receita de cada uma), não mais a MC% de uma
/// população só (fato_margem_produto, só Comercio) aplicada à receita total.
/// </summary>
public sealed class PontoDeEquilibrioServiceTests
{
    private const string BusinessId = "biz-breakeven-mix";

    private sealed record Ambiente(
        PontoDeEquilibrioService Servico,
        InMemoryContaAReceberRepository ContasAReceber,
        InMemoryContaAPagarRepository ContasAPagar,
        InMemoryFatoCustoDiarioRepository FatoCustoDiario,
        InMemoryFatoReceitaDiariaRepository FatoReceitaDiaria,
        InMemoryRecorrenciaRepository Recorrencias,
        FakeRelogio Relogio);

    private static Ambiente NovoAmbiente(DateTimeOffset hoje)
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();
        var fatoReceitaDiaria = new InMemoryFatoReceitaDiariaRepository();
        var recorrencias = new InMemoryRecorrenciaRepository();
        var relogio = new FakeRelogio(hoje);

        var dre = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var servico = new PontoDeEquilibrioService(recorrencias, fatoReceitaDiaria, dre, relogio);

        return new Ambiente(servico, contasAReceber, contasAPagar, fatoCustoDiario, fatoReceitaDiaria, recorrencias, relogio);
    }

    /// <summary>
    /// Mix com as 3 correntes, cada uma com sua própria economia unitária: Recorrente MC 100%
    /// (assinatura, sem custo direto), Servico MC 80% (comissão de 20% sobre a receita), Comercio
    /// MC 30% (CMV real de 70%). MC% do MIX = Σ margens ÷ Σ receitas — NUNCA a MC% de uma corrente
    /// isolada (ex.: só a de Comercio, 30%) aplicada à receita total (que superestimaria a receita
    /// necessária, já que ignora a MC alta das outras duas correntes).
    /// </summary>
    [Fact]
    public async Task CalcularAsync_ComReceitaNasTresCorrentes_UsaMcBlendedPorMixNaoAMcDeUmaCorrenteSo()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        // Recorrente: R$100, sem custo direto -> M=100.
        var assinatura = ContaAReceber.Criar(
            BusinessId, new SourceRef("assinatura", "as1"), "Plano", CategoriaFinanceiraPadrao.ReceitaRecorrente,
            hoje, Money.DeReais(100), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), hoje),
            corrente: CorrenteDeReceita.Recorrente).Valor;
        await ambiente.ContasAReceber.SalvarAsync(assinatura);

        // Servico: R$200, comissão de R$40 (20%) -> M=160.
        var os = ContaAReceber.Criar(
            BusinessId, new SourceRef("appointment", "os1"), "OS", CategoriaFinanceiraPadrao.Servicos,
            hoje, Money.DeReais(200), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(200), hoje),
            corrente: CorrenteDeReceita.Servico).Valor;
        await ambiente.ContasAReceber.SalvarAsync(os);
        var comissao = ContaAPagar.Criar(
            BusinessId, new SourceRef("teste", "comissao1"), "Comissão", CategoriaFinanceiraPadrao.Comissoes,
            hoje, Money.DeReais(40), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(40), hoje),
            corrente: CorrenteDeReceita.Servico).Valor;
        await ambiente.ContasAPagar.SalvarAsync(comissao);

        // Comercio: R$300, CMV real de R$210 (70%) -> M=90.
        var venda = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda1"), "Venda", CategoriaFinanceiraPadrao.Servicos,
            hoje, Money.DeReais(300), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(300), hoje),
            corrente: CorrenteDeReceita.Comercio).Valor;
        await ambiente.ContasAReceber.SalvarAsync(venda);
        await ambiente.FatoCustoDiario.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Comercio, Money.DeReais(210).Centavos);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId);

        // Σ receita = 600; Σ margem = 100 + 160 + 90 = 350 -> MC% do mix = 350/600 ≈ 58,333%.
        Assert.Equal(350.0 / 600.0, resultado.MargemContribuicaoPercentual, precision: 10);

        // NUNCA a MC% de uma corrente isolada: nem a de Comercio (30%, a mais baixa — usá-la
        // sozinha SUPERESTIMARIA a receita necessária), nem a de Recorrente (100%, a mais alta).
        Assert.NotEqual(0.30, resultado.MargemContribuicaoPercentual, precision: 6);
        Assert.NotEqual(1.00, resultado.MargemContribuicaoPercentual, precision: 6);
    }

    [Fact]
    public async Task CalcularAsync_SemReceitaNaJanela_McPercentualCaiParaZero()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId);

        Assert.Equal(0, resultado.MargemContribuicaoPercentual);
    }

    /// <summary>Custo de oportunidade repassado ao PE econômico do <see cref="PontoDeEquilibrioResultado"/>
    /// — orquestração de ponta a ponta do enriquecimento do matemonstro (unit da fórmula em si já
    /// coberta por <c>BreakevenMensalTests</c>).</summary>
    [Fact]
    public async Task CalcularAsync_ComCustoDeOportunidade_ElevaAReceitaNecessariaEconomicaAcimaDaContabil()
    {
        var hoje = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(hoje);
        var hojeData = DateOnly.FromDateTime(hoje.UtcDateTime);

        var recorrencia = RecorrenciaAgg.Criar(
            BusinessId, "Aluguel", TipoContaRecorrente.APagar, Money.DeReais(1_000),
            "aluguel", FrequenciaRecorrencia.Mensal, hoje.AddMonths(-6)).Valor;
        await ambiente.Recorrencias.SalvarAsync(recorrencia);

        var venda = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda1"), "Venda", CategoriaFinanceiraPadrao.Servicos,
            hoje, Money.DeReais(1_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), hoje),
            corrente: CorrenteDeReceita.Comercio).Valor;
        await ambiente.ContasAReceber.SalvarAsync(venda);
        await ambiente.FatoCustoDiario.AcumularAsync(BusinessId, hojeData, CorrenteDeReceita.Comercio, Money.DeReais(500).Centavos);

        var semOportunidade = await ambiente.Servico.CalcularAsync(BusinessId);
        var comOportunidade = await ambiente.Servico.CalcularAsync(BusinessId, custoDeOportunidadeMensalCentavos: 500_00);

        Assert.Equal(semOportunidade.ReceitaNecessariaMensalCentavos, semOportunidade.ReceitaNecessariaMensalEconomicaCentavos);
        Assert.True(comOportunidade.ReceitaNecessariaMensalEconomicaCentavos > comOportunidade.ReceitaNecessariaMensalCentavos);
    }
}
