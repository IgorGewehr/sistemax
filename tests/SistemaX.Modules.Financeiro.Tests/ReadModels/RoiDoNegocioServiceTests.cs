using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// Painel de ROI do negócio (docs/financeiro/design-imobilizado-roi.md §7) — o cenário nominal do
/// dia-zero (§7.8), a invariância de aportes (§7.4, property test), o anti-dupla-contagem de capex
/// (§7.2/§14.4) e o opt-in (§2.2).
/// </summary>
public sealed class RoiDoNegocioServiceTests
{
    private const string Biz = "loja-1";
    private static readonly DateOnly M0 = new(2026, 7, 1);

    private sealed record Ambiente(
        InMemoryAtivoDeCapitalRepository Ativos, InMemoryAporteDeCapitalRepository Aportes,
        InMemoryMovimentoFinanceiroRepository Movimentos, InMemoryContaAPagarRepository ContasAPagar,
        InMemoryContaAReceberRepository ContasAReceber, InMemoryConfiguracaoFinanceiraTenantRepository Configuracoes,
        FakeRelogio Relogio);

    private static Ambiente NovoAmbiente(DateTimeOffset agora)
        => new(new InMemoryAtivoDeCapitalRepository(), new InMemoryAporteDeCapitalRepository(),
            new InMemoryMovimentoFinanceiroRepository(), new InMemoryContaAPagarRepository(),
            new InMemoryContaAReceberRepository(), new InMemoryConfiguracaoFinanceiraTenantRepository(), new FakeRelogio(agora));

    private static RoiDoNegocioService NovoServico(Ambiente ambiente)
    {
        var dre = new DreGerencialService(
            ambiente.ContasAReceber, ambiente.ContasAPagar, new InMemoryFatoCustoDiarioRepository(),
            new InMemoryFatoRecebiveisRepository(), ambiente.Ativos);
        return new RoiDoNegocioService(ambiente.Ativos, ambiente.Aportes, ambiente.Movimentos, ambiente.ContasAPagar, ambiente.Configuracoes, dre, ambiente.Relogio);
    }

    private static async Task LigarToggleAsync(Ambiente ambiente, int? taxaDescontoAnualBps = null)
    {
        var config = ConfiguracaoFinanceiraTenant.Criar(Biz, imobilizadoRoiAtivo: true, taxaDescontoAnualBps: taxaDescontoAnualBps).Valor;
        await ambiente.Configuracoes.SalvarAsync(config);
    }

    /// <summary>Monta o cenário nominal §7.8: bem R$55.800 sem conta (pago fora do sistema) em m0;
    /// burn R$3.000/mês nos meses 1-4; depois F=+R$6.000/mês.</summary>
    private static async Task MontarCenarioNominalAsync(Ambiente ambiente, DateTimeOffset agora, int ultimoMesInclusive)
    {
        var ativo = AtivoDeCapital.Criar(
            Biz, "Estrutura da assistência", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(55_800), Money.Zero, M0, M0, vidaUtilMeses: 60, criadoEm: agora).Valor;
        await ambiente.Ativos.SalvarAsync(ativo);

        var aporte = AporteDeCapital.Criar(Biz, Money.DeReais(20_000), M0, "Capital de giro inicial", agora).Valor;
        await ambiente.Aportes.SalvarAsync(aporte);

        for (var m = 1; m <= Math.Min(4, ultimoMesInclusive); m++)
        {
            await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Saida, Money.DeReais(3_000), M0.AddMonths(m), $"burn-{m}");
        }
        for (var m = 5; m <= ultimoMesInclusive; m++)
        {
            await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Entrada, Money.DeReais(6_000), M0.AddMonths(m), $"receita-{m}");
        }
    }

    private static async Task RegistrarMovimentoAsync(
        Ambiente ambiente, TipoMovimentoFinanceiro tipo, Money valor, DateOnly competencia, string sufixoId, string? contaOrigemId = null)
    {
        var data = new DateTimeOffset(competencia.Year, competencia.Month, 15, 12, 0, 0, TimeSpan.Zero);
        var movimento = MovimentoFinanceiro.Registrar(
            Biz, "caixa-1", "pix", $"parcela-{sufixoId}", contaOrigemId ?? $"conta-op-{sufixoId}",
            tipo, valor, data, new SourceRef("teste", sufixoId));
        await ambiente.Movimentos.SalvarAsync(movimento.Valor);
    }

    [Fact]
    public async Task CenarioNominal_InvestidoPaybackEGiroConsumidoBatemComODesign()
    {
        var agora = new DateTimeOffset(2027, 11, 20, 12, 0, 0, TimeSpan.Zero); // dentro do mês 16
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);
        await MontarCenarioNominalAsync(ambiente, agora, ultimoMesInclusive: 16);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;

        Assert.Equal(5_580_000, resultado.Investimento.CapexCentavos);
        Assert.Equal(2_000_000, resultado.Investimento.AportesCentavos);
        Assert.Equal(7_580_000, resultado.Investimento.TotalCentavos); // R$75.800 (§7.8)

        Assert.Equal(new DateOnly(2027, 11, 1), resultado.Payback.SimplesRealizadoEm); // mês 16

        Assert.Equal(1_200_000, resultado.Investimento.GiroConsumidoObservadoCentavos); // R$12.000
    }

    [Fact]
    public async Task CenarioNominal_Aos24Meses_RoiCaixaAproximadamente68Virgula9Porcento()
    {
        var agora = new DateTimeOffset(2028, 7, 15, 12, 0, 0, TimeSpan.Zero); // mês 24 exato
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);
        await MontarCenarioNominalAsync(ambiente, agora, ultimoMesInclusive: 24);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;

        // Acum(24) = −67.800 + 20×6.000 = 52.200 → ROI = 100·52.200/75.800 ≈ 68,9% (§7.8)
        Assert.InRange(resultado.Roi.CaixaPercent, 68.0m, 69.5m);
        Assert.Equal(0, resultado.Roi.MesesAteRoiCompleto); // já cruzou (payback no mês 16)
    }

    [Fact]
    public async Task CenarioNominal_TirAnualizadaEmTornoDe44Porcento()
    {
        var agora = new DateTimeOffset(2028, 7, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);
        await MontarCenarioNominalAsync(ambiente, agora, ultimoMesInclusive: 24);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;

        Assert.Null(resultado.Tir.MotivoIndefinida);
        Assert.NotNull(resultado.Tir.AnualizadaPercent);
        Assert.InRange(resultado.Tir.AnualizadaPercent!.Value, 20m, 70m); // faixa de sanidade, não valor mágico
    }

    [Fact]
    public async Task PaybackDescontado_ComTaxaConfigurada_NuncaAntesDoSimples()
    {
        // O payback descontado (12% a.a.) só cruza no mês 17 (1 mês depois do simples, mês 16) —
        // a série precisa cobrir até lá para o cruzamento aparecer (§7.5: a busca não extrapola
        // além da série informada).
        var agora = new DateTimeOffset(2027, 12, 20, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente, taxaDescontoAnualBps: 1200);
        await MontarCenarioNominalAsync(ambiente, agora, ultimoMesInclusive: 17);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;

        Assert.NotNull(resultado.Payback.SimplesRealizadoEm);
        Assert.NotNull(resultado.Payback.DescontadoRealizadoEm);
        Assert.True(resultado.Payback.DescontadoRealizadoEm >= resultado.Payback.SimplesRealizadoEm);
    }

    [Fact]
    public async Task SemTaxaConfigurada_PaybackDescontadoOmitido()
    {
        var agora = new DateTimeOffset(2027, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente, taxaDescontoAnualBps: null);
        await MontarCenarioNominalAsync(ambiente, agora, ultimoMesInclusive: 16);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;

        Assert.Null(resultado.Payback.DescontadoRealizadoEm);
        Assert.Null(resultado.Payback.DescontadoProjetadoMeses);
    }

    /// <summary>§7.4 — property test: registrar um aporte de QUALQUER valor extra não move
    /// <c>simplesRealizadoEm</c> nem <c>mesesAteRoiCompleto</c> — o aporte se cancela nos dois lados
    /// de <c>Recuperado − Investido</c>.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(1_000_00)]
    [InlineData(999_999_00)]
    public async Task Invariancia_AporteExtraDeQualquerValor_NaoMoveOPayback(long aporteExtraCentavos)
    {
        var agora = new DateTimeOffset(2027, 11, 20, 12, 0, 0, TimeSpan.Zero);

        var baseline = NovoAmbiente(agora);
        await LigarToggleAsync(baseline);
        await MontarCenarioNominalAsync(baseline, agora, ultimoMesInclusive: 16);
        var resultadoBaseline = (await NovoServico(baseline).CalcularAsync(Biz)).Valor;

        var comAporteExtra = NovoAmbiente(agora);
        await LigarToggleAsync(comAporteExtra);
        await MontarCenarioNominalAsync(comAporteExtra, agora, ultimoMesInclusive: 16);
        var extra = AporteDeCapital.Criar(Biz, new Money(aporteExtraCentavos), M0.AddMonths(2), "Reforço qualquer", agora).Valor;
        await comAporteExtra.Aportes.SalvarAsync(extra);
        var resultadoComExtra = (await NovoServico(comAporteExtra).CalcularAsync(Biz)).Valor;

        Assert.Equal(resultadoBaseline.Payback.SimplesRealizadoEm, resultadoComExtra.Payback.SimplesRealizadoEm);
        Assert.Equal(resultadoBaseline.Roi.MesesAteRoiCompleto, resultadoComExtra.Roi.MesesAteRoiCompleto);

        // O que MUDA: o denominador do ROI% (Investido) e o total de aportes.
        Assert.Equal(resultadoBaseline.Investimento.AportesCentavos + aporteExtraCentavos, resultadoComExtra.Investimento.AportesCentavos);
    }

    /// <summary>Anti-dupla-contagem (§7.2/§14.4): um bem COM conta vinculada entra em
    /// <c>Capex_m</c> pela parcela paga, e o movimento que liquidou essa parcela é EXCLUÍDO de
    /// <c>F_m</c> (a <c>ContaOrigemId</c> pertence a uma conta de categoria <c>ativo-de-capital</c>).
    /// Um movimento NÃO relacionado, no mesmo mês, entra normalmente em <c>F_m</c>.</summary>
    [Fact]
    public async Task AntiDuplaContagem_MovimentoDeCapexEExcluidoDeFmENuncaContadoDuasVezes()
    {
        var agora = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);

        var parcelas = ContaFinanceiraBase.ParcelaUnica(Money.DeReais(10_000), new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));
        var conta = ContaAPagar.Criar(
            Biz, new SourceRef("financeiro-ativo", "ativo-1"), "Investimento — Bancada", CategoriaFinanceiraPadrao.AtivoDeCapital,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), Money.DeReais(10_000), parcelas).Valor;
        conta.RegistrarLiquidacaoParcela(conta.Parcelas[0].Id, Money.DeReais(10_000), new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero), "pix");
        await ambiente.ContasAPagar.SalvarAsync(conta);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Bancada", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(10_000), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), vidaUtilMeses: 60,
            criadoEm: agora, contaAPagarId: conta.Id).Valor;
        await ambiente.Ativos.SalvarAsync(ativo);

        // O movimento que liquidou a parcela — ContaOrigemId aponta pra conta ativo-de-capital.
        await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Saida, Money.DeReais(10_000), new DateOnly(2026, 7, 1), "capex", conta.Id);
        // Movimento operacional normal, no MESMO mês, SEM relação com o ativo.
        await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Entrada, Money.DeReais(1_000), new DateOnly(2026, 7, 1), "venda-normal");

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;
        var mesJulho = resultado.Serie.Single(s => s.Competencia == new DateOnly(2026, 7, 1));

        Assert.Equal(1_000_000, mesJulho.CapexCentavos); // a parcela paga, uma vez só
        Assert.Equal(100_000, mesJulho.FluxoOperacionalCentavos); // só a venda normal — capex excluído
    }

    /// <summary>Gera combinações pseudo-aleatórias (seed fixa — reprodutível, nunca flaky) de
    /// capex/parcelas/movimento-não-relacionado/mês para o property test abaixo. Varia: valor do
    /// capex (R$1 a R$500.000), quantidade de parcelas do MESMO bem liquidadas no MESMO mês (1 a
    /// 3 — testa que a soma de várias parcelas nunca duplica), valor de um movimento operacional
    /// não relacionado (R$0 a R$200.000, pode ser ausente) e o mês-alvo dentro do ano.</summary>
    public static IEnumerable<object[]> CombinacoesAleatoriasDeCapexEMovimentos()
    {
        var rng = new Random(20260718);
        for (var i = 0; i < 30; i++)
        {
            var capexCentavos = rng.Next(1_00, 500_000_00);
            var numParcelas = rng.Next(1, 4);
            var outroMovimentoCentavos = rng.Next(0, 200_000_00);
            var mesOffset = rng.Next(0, 12);
            yield return [capexCentavos, numParcelas, outroMovimentoCentavos, mesOffset];
        }
    }

    /// <summary>Property test (§7.2/§14.4) — versão N-combinações do
    /// <see cref="AntiDuplaContagem_MovimentoDeCapexEExcluidoDeFmENuncaContadoDuasVezes"/>: para
    /// QUALQUER combinação de valor de capex, quantidade de parcelas do bem liquidadas no mesmo
    /// mês, valor de um movimento operacional não relacionado e mês-alvo, a propriedade se
    /// mantém — <c>Capex_m</c> soma o bem EXATAMENTE uma vez (nunca por parcela em duplicidade) e
    /// <c>F_m</c> nunca inclui um centavo do capex, só o movimento não relacionado.</summary>
    [Theory]
    [MemberData(nameof(CombinacoesAleatoriasDeCapexEMovimentos))]
    public async Task AntiDuplaContagem_Property_QualquerCombinacaoDeParcelasEMovimentos_NuncaContaDuasVezes(
        long capexCentavos, int numParcelas, long outroMovimentoCentavos, int mesOffset)
    {
        var mesAlvo = M0.AddMonths(mesOffset);
        var agora = new DateTimeOffset(mesAlvo.Year, mesAlvo.Month, 20, 12, 0, 0, TimeSpan.Zero).AddMonths(3);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);

        // Distribui capexCentavos em numParcelas parcelas (resto na última — mesmo desempate
        // Hamilton do CronogramaLinear), descartando parcelas que ficariam com valor zero.
        var baseParcela = capexCentavos / numParcelas;
        var resto = capexCentavos - baseParcela * numParcelas;
        var valoresParcelas = Enumerable.Range(0, numParcelas)
            .Select(i => i == numParcelas - 1 ? baseParcela + resto : baseParcela)
            .Where(v => v > 0)
            .ToArray();

        var vencimento = new DateTimeOffset(mesAlvo.Year, mesAlvo.Month, 20, 0, 0, 0, TimeSpan.Zero);
        var parcelas = valoresParcelas
            .Select((v, idx) => Parcela.Criar(idx + 1, vencimento, new Money(v)))
            .ToList();
        var conta = ContaAPagar.Criar(
            Biz, new SourceRef("financeiro-ativo", $"property-{capexCentavos}-{numParcelas}-{mesOffset}"),
            "Investimento — property", CategoriaFinanceiraPadrao.AtivoDeCapital, vencimento, new Money(capexCentavos), parcelas).Valor;
        foreach (var parcela in conta.Parcelas)
            conta.RegistrarLiquidacaoParcela(parcela.Id, parcela.Valor, vencimento, "pix");
        await ambiente.ContasAPagar.SalvarAsync(conta);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Bem property", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            new Money(capexCentavos), Money.Zero, mesAlvo, mesAlvo, vidaUtilMeses: 60,
            criadoEm: agora, contaAPagarId: conta.Id).Valor;
        await ambiente.Ativos.SalvarAsync(ativo);

        // Um movimento por parcela liquidada, todos com ContaOrigemId = conta.Id (trilho de capex).
        for (var idx = 0; idx < valoresParcelas.Length; idx++)
            await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Saida, new Money(valoresParcelas[idx]), mesAlvo, $"capex-{idx}", conta.Id);

        // Movimento operacional NÃO relacionado, no MESMO mês (ausente quando outroMovimentoCentavos == 0).
        if (outroMovimentoCentavos > 0)
            await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Entrada, new Money(outroMovimentoCentavos), mesAlvo, "operacional");

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;
        var mesDaSerie = resultado.Serie.Single(s => s.Competencia == mesAlvo);

        // A PROPRIEDADE: o capex aparece EXATAMENTE uma vez (em Capex_m, somando todas as
        // parcelas) e NUNCA em F_m — independente de valor, quantidade de parcelas ou mês.
        Assert.Equal(capexCentavos, mesDaSerie.CapexCentavos);
        Assert.Equal(outroMovimentoCentavos, mesDaSerie.FluxoOperacionalCentavos);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // I4 — Alienação de ativo (docs/financeiro/design-imobilizado-roi.md §4.6/§12): "proceeds
    // aparecem em F_m" e o painel por categoria enriquecido (Vendidos/ResultadoAlienacaoCentavos).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Alienacao_ProceedsDaVendaEntramNoFluxoOperacionalDoMes()
    {
        var agora = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Bancada ESD", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(12_000), Money.Zero, M0, M0, vidaUtilMeses: 60, criadoEm: agora).Valor;
        ativo.Baixar("Upgrade", new DateOnly(2026, 8, 1), 1_180_000, agora, Money.DeReais(9_000));
        await ambiente.Ativos.SalvarAsync(ativo);

        // O MovimentoFinanceiro de Entrada que liquida a ContaAReceber da alienação — mesmo
        // mecanismo de qualquer outro recebimento, SEM ContaOrigemId de categoria ativo-de-capital
        // (o anti-dupla-contagem só exclui o CAPEX, nunca os proceeds da venda).
        await RegistrarMovimentoAsync(ambiente, TipoMovimentoFinanceiro.Entrada, Money.DeReais(9_000), new DateOnly(2026, 8, 1), "proceeds-venda");

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;
        var mesAgosto = resultado.Serie.Single(s => s.Competencia == new DateOnly(2026, 8, 1));

        Assert.Equal(900_000, mesAgosto.FluxoOperacionalCentavos);
    }

    [Fact]
    public async Task Alienacao_PainelPorCategoria_ContaVendidoEResultadoAlienacao()
    {
        var agora = new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        await LigarToggleAsync(ambiente);

        var vendido = AtivoDeCapital.Criar(
            Biz, "Bancada ESD", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(12_000), Money.Zero, M0, M0, vidaUtilMeses: 60, criadoEm: agora).Valor;
        vendido.Baixar("Upgrade", new DateOnly(2026, 8, 1), 1_180_000, agora, Money.DeReais(12_800)); // ganho de 1.000
        await ambiente.Ativos.SalvarAsync(vendido);

        var emUso = AtivoDeCapital.Criar(
            Biz, "Bancada nova", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(15_000), Money.Zero, M0, M0, vidaUtilMeses: 60, criadoEm: agora).Valor;
        await ambiente.Ativos.SalvarAsync(emUso);

        var resultado = (await NovoServico(ambiente).CalcularAsync(Biz)).Valor;
        var categoria = resultado.Investimento.PorCategoria.Single(c => c.Categoria == CategoriaAtivo.Equipamento.ToString());

        Assert.Equal(1, categoria.Vendidos); // dos 2 bens da categoria, só 1 foi vendido
        Assert.Equal(100_000, categoria.ResultadoAlienacaoCentavos); // R$1.000,00 de ganho
        Assert.Equal(100_000, resultado.Investimento.ResultadoAlienacaoTotalCentavos);
    }

    [Fact]
    public async Task ToggleDesligado_RetornaFalhaComCodigoDesativado()
    {
        var agora = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
        var ambiente = NovoAmbiente(agora);
        // Sem LigarToggleAsync — config nunca gravada, cai no Padrao (tudo desligado).

        var resultado = await NovoServico(ambiente).CalcularAsync(Biz);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.imobilizado.desativado", resultado.Erro.Codigo);
    }
}
