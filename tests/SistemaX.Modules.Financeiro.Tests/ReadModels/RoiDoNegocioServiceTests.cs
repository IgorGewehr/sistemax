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
