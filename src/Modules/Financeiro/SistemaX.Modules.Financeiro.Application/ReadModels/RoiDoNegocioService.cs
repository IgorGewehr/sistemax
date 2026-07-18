using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Configuracao;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma linha da série mensal (§7.1 "serie" — o rastreio mês a mês que o quant audita:
/// reproduz <c>N_m</c> termo a termo contra movimentos/bens/aportes).</summary>
public sealed record RoiSerieMensal(
    DateOnly Competencia, long FluxoOperacionalCentavos, long CapexCentavos, long AporteCentavos,
    long LiquidoCentavos, long AcumuladoCentavos, long AcumuladoDescontadoCentavos);

/// <summary>Fatia I4 (§12 "painel por categoria de bem enriquecido") — <c>Vendidos</c> e
/// <c>ResultadoAlienacaoCentavos</c> são aditivos (0 até a primeira venda da categoria, nunca
/// mudam o shape pré-I4). <c>ResultadoAlienacaoCentavos</c> soma <see cref="Domain.Ativos.AtivoDeCapital.ResultadoAlienacaoCentavos"/>
/// de todo bem <c>Vendido</c> da categoria — ganho ou perda acumulados, informativo (mesma
/// natureza da linha do DRE, §4.7, DI6: nunca entra em nenhum percentual de ROI operacional).</summary>
public sealed record RoiPorCategoria(string Categoria, long CustoCentavos, long ValorContabilCentavos, int Vendidos, long ResultadoAlienacaoCentavos);

public sealed record RoiInvestimento(
    long CapexCentavos, long AportesCentavos, long TotalCentavos, long GiroConsumidoObservadoCentavos,
    int Bens, IReadOnlyList<RoiPorCategoria> PorCategoria, long ResultadoAlienacaoTotalCentavos);

public sealed record RoiRecuperacao(long FluxoOperacionalAcumuladoCentavos, long RecuperadoCentavos, long FaltamCentavos, decimal PercentRecuperado);

public sealed record RoiPayback(
    DateOnly? SimplesRealizadoEm, DateOnly? DescontadoRealizadoEm, int? ProjetadoMeses, int? DescontadoProjetadoMeses, string Metodo);

public sealed record RoiTir(decimal? MensalPercent, decimal? AnualizadaPercent, string? MotivoIndefinida);

public sealed record RoiPercentuais(decimal CaixaPercent, decimal CompetenciaPercent, int? MesesAteRoiCompleto);

public sealed record RoiDoNegocioResultado(
    DateOnly MarcoInicial, int? TaxaDescontoAnualBps, RoiInvestimento Investimento, RoiRecuperacao Recuperacao,
    RoiPayback Payback, RoiTir Tir, RoiPercentuais Roi, IReadOnlyList<RoiSerieMensal> Serie);

/// <summary>
/// PAINEL DE ROI DO NEGÓCIO (docs/financeiro/design-imobilizado-roi.md §7) —
/// <c>GET /financeiro/roi-negocio</c>. Read-model: SÓ montagem de fontes + chamadas ao Quant
/// (<see cref="MatematicaDePayback"/>/<see cref="TaxaInternaDeRetorno"/>) — zero fórmula própria
/// além de Σ (§10: "o lar único de cada fórmula"). Fontes: <c>MovimentoFinanceiro</c> (caixa
/// operacional bilateral — nunca <c>fato_caixa_diario</c>, unilateral), <c>AtivoDeCapital</c>
/// (capex/depreciação), <c>AporteDeCapital</c> (funding) e <c>DreGerencialService</c> (lucro por
/// competência, mesma fonte do DRE — nunca uma segunda conta de lucro).
/// </summary>
public sealed class RoiDoNegocioService(
    IAtivoDeCapitalRepository ativosRepo, IAporteDeCapitalRepository aportesRepo,
    IMovimentoFinanceiroRepository movimentosRepo, IContaAPagarRepository contasAPagarRepo,
    IConfiguracaoFinanceiraTenantRepository configuracoes, DreGerencialService dre, IRelogio relogio)
{
    private const int HorizonteProjecaoMeses = 120;
    private const string MetodoPayback = "simulacao-fluxo-conhecido";

    public async Task<Result<RoiDoNegocioResultado>> CalcularAsync(string businessId, CancellationToken ct = default)
    {
        var gating = await FinanceiroOptInGuard.ExigirImobilizadoRoiAsync(businessId, configuracoes, ct).ConfigureAwait(false);
        if (gating.Falha) return Result.Falhar<RoiDoNegocioResultado>(gating.Erro);

        var config = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora.Date);
        var mesAtual = new DateOnly(hoje.Year, hoje.Month, 1);

        var ativos = await ativosRepo.ListarAsync(businessId, ct: ct).ConfigureAwait(false);
        var aportes = await aportesRepo.ListarAsync(businessId, ct).ConfigureAwait(false);
        var contasAtivoDeCapital = await contasAPagarRepo
            .ListarPorCategoriaAsync(businessId, CategoriaFinanceiraPadrao.AtivoDeCapital, ct).ConfigureAwait(false);

        var m0 = ResolverMarcoInicial(config?.InicioOperacao, ativos, aportes, mesAtual);

        var movimentos = await movimentosRepo
            .ListarPorPeriodoAsync(businessId, m0.InicioDoDia(), mesAtual.AddMonths(1).AddDays(-1).FimDoDia(), ct).ConfigureAwait(false);

        var idsContasAtivo = contasAtivoDeCapital.Select(c => c.Id).ToHashSet();

        var meses = MesesEntre(m0, mesAtual);
        var serie = new List<RoiSerieMensal>(meses.Count);

        var taxaMensal = ResolverTaxaMensal(config?.TaxaDescontoAnualBps);

        long acumulado = 0;
        decimal acumuladoDescontado = 0;
        long fluxoOperacionalAcumulado = 0;
        long picoDeBurn = 0; // menor valor (mais negativo) do fluxo operacional acumulado — GiroConsumidoObservado
        long runningF = 0;

        for (var indice = 0; indice < meses.Count; indice++)
        {
            var competencia = meses[indice];
            var (inicioMes, fimMes) = LimitesDoMes(competencia);

            var movimentosDoMes = movimentos.Where(mv => mv.DataMovimento >= inicioMes && mv.DataMovimento <= fimMes).ToList();
            var fluxoOperacional = CalcularFluxoOperacional(movimentosDoMes, idsContasAtivo);

            var capexMes = CalcularCapexDoMes(contasAtivoDeCapital, ativos, competencia);
            var aporteMes = aportes.Where(a => MesDe(a.Data) == competencia).Sum(a => a.Valor.Centavos);

            var liquido = fluxoOperacional - capexMes;
            acumulado += liquido;

            var fatorDesconto = (decimal)Math.Pow((double)(1 + taxaMensal), -indice);
            acumuladoDescontado += liquido * fatorDesconto;

            runningF += fluxoOperacional;
            if (runningF < picoDeBurn) picoDeBurn = runningF;
            fluxoOperacionalAcumulado += fluxoOperacional;

            serie.Add(new RoiSerieMensal(
                competencia, fluxoOperacional, capexMes, aporteMes, liquido, acumulado, (long)Math.Round(acumuladoDescontado, MidpointRounding.ToEven)));
        }

        var giroConsumidoObservado = Math.Max(0, -picoDeBurn);

        var investimento = CalcularInvestimento(ativos, aportes, hoje, giroConsumidoObservado);
        var recuperacao = CalcularRecuperacao(investimento, fluxoOperacionalAcumulado);

        var serieParaPayback = serie.Select(s => (s.Competencia, s.LiquidoCentavos)).ToList();
        var paybackSimples = MatematicaDePayback.PaybackSimples(serieParaPayback);
        var paybackDescontado = config?.TaxaDescontoAnualBps is null
            ? (DateOnly?)null
            : MatematicaDePayback.PaybackDescontado(serieParaPayback, taxaMensal);

        var (parcelasAbertasPorMes, margemCaixaMensal) = PrepararProjecao(contasAtivoDeCapital, serie, mesAtual);

        var projetadoMeses = MatematicaDePayback.ProjetarCruzamento(
            acumulado, k => margemCaixaMensal - parcelasAbertasPorMes.GetValueOrDefault(mesAtual.AddMonths(k), 0L), HorizonteProjecaoMeses);

        int? descontadoProjetadoMeses = null;
        if (config?.TaxaDescontoAnualBps is not null)
        {
            var indiceHoje = meses.Count - 1;
            descontadoProjetadoMeses = MatematicaDePayback.ProjetarCruzamento(
                acumuladoDescontado,
                k => (margemCaixaMensal - parcelasAbertasPorMes.GetValueOrDefault(mesAtual.AddMonths(k), 0L)) *
                     (decimal)Math.Pow((double)(1 + taxaMensal), -(indiceHoje + k)),
                HorizonteProjecaoMeses);
        }

        var payback = new RoiPayback(paybackSimples, paybackDescontado, projetadoMeses, descontadoProjetadoMeses, MetodoPayback);

        var tir = TaxaInternaDeRetorno.Calcular(serie.Select(s => s.LiquidoCentavos).ToList());

        var roiPercentuais = await CalcularRoiPercentuaisAsync(businessId, m0, agora, investimento.TotalCentavos, acumulado, projetadoMeses, ct).ConfigureAwait(false);

        return Result.Ok(new RoiDoNegocioResultado(
            m0, config?.TaxaDescontoAnualBps,
            investimento, recuperacao,
            payback, new RoiTir(tir.MensalPercent, tir.AnualizadaPercent, tir.MotivoIndefinida), roiPercentuais,
            serie));
    }

    private static DateOnly ResolverMarcoInicial(
        DateOnly? inicioOperacaoConfigurado, IReadOnlyList<AtivoDeCapital> ativos, IReadOnlyList<AporteDeCapital> aportes, DateOnly mesAtual)
    {
        if (inicioOperacaoConfigurado is { } configurado) return MesDe(configurado);

        DateOnly? primeiroFato = null;
        foreach (var ativo in ativos)
        {
            var mes = MesDe(ativo.DataAquisicao);
            if (primeiroFato is null || mes < primeiroFato) primeiroFato = mes;
        }
        foreach (var aporte in aportes)
        {
            var mes = MesDe(aporte.Data);
            if (primeiroFato is null || mes < primeiroFato) primeiroFato = mes;
        }

        return primeiroFato ?? mesAtual;
    }

    private static List<DateOnly> MesesEntre(DateOnly m0, DateOnly mesAtual)
    {
        var lista = new List<DateOnly>();
        var cursor = m0;
        while (cursor <= mesAtual)
        {
            lista.Add(cursor);
            cursor = cursor.AddMonths(1);
        }
        return lista;
    }

    private static (DateTimeOffset Inicio, DateTimeOffset Fim) LimitesDoMes(DateOnly competencia)
    {
        var inicio = new DateTimeOffset(competencia.Year, competencia.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = inicio.AddMonths(1).AddTicks(-1);
        return (inicio, fim);
    }

    private static DateOnly MesDe(DateOnly data) => new(data.Year, data.Month, 1);

    /// <summary>F_m (§7.2): Σ movimentos do mês, EXCLUINDO qualquer um cuja <c>ContaOrigemId</c>
    /// pertença a uma conta de categoria <c>ativo-de-capital</c> — o anti-dupla-contagem (o caixa do
    /// capex já está em <c>Capex_m</c>).</summary>
    private static long CalcularFluxoOperacional(IReadOnlyList<MovimentoFinanceiro> movimentosDoMes, HashSet<string> idsContasAtivo)
        => movimentosDoMes
            .Where(mv => !idsContasAtivo.Contains(mv.ContaOrigemId))
            .Sum(mv => mv.Tipo == TipoMovimentoFinanceiro.Entrada ? mv.Valor.Centavos : -mv.Valor.Centavos);

    /// <summary>Capex_m (§7.2, DI7): trilho A (parcelas PAGAS no mês de contas categoria
    /// ativo-de-capital — bem COM conta) + trilho B (custo integral dos bens SEM
    /// <c>ContaAPagarId</c>, no mês da aquisição — bem pago fora do sistema). Invariante: cada bem
    /// entra por EXATAMENTE um trilho.</summary>
    private static long CalcularCapexDoMes(IReadOnlyList<Domain.ContasAPagarReceber.ContaAPagar> contasAtivoDeCapital, IReadOnlyList<AtivoDeCapital> ativos, DateOnly competencia)
    {
        var trilhoA = contasAtivoDeCapital
            .SelectMany(c => c.Parcelas)
            .Where(p => p.TemPagamentoRegistrado && p.DataLiquidacao is { } liq && MesDe(DateOnly.FromDateTime(liq.Date)) == competencia)
            .Sum(p => p.ValorPago.Centavos);

        var trilhoB = ativos
            .Where(a => a.ContaAPagarId is null && MesDe(a.DataAquisicao) == competencia)
            .Sum(a => a.CustoAquisicao.Centavos);

        return trilhoA + trilhoB;
    }

    /// <summary>Investido(T) (§7.2): o capex entra pelo CUSTO na aquisição, independente do
    /// parcelamento — "quanto foi comprometido", nunca "quanto já saiu do caixa" (essa é a série
    /// temporal, <see cref="CalcularCapexDoMes"/>).</summary>
    private static RoiInvestimento CalcularInvestimento(
        IReadOnlyList<AtivoDeCapital> ativos, IReadOnlyList<AporteDeCapital> aportes, DateOnly hoje, long giroConsumidoObservado)
    {
        var considerados = ativos.Where(a => a.DataAquisicao <= hoje).ToList();
        var capexCentavos = considerados.Sum(a => a.CustoAquisicao.Centavos);
        var aportesCentavos = aportes.Where(a => a.Data <= hoje).Sum(a => a.Valor.Centavos);

        var porCategoria = considerados
            .GroupBy(a => a.Categoria)
            .Select(g => new RoiPorCategoria(
                g.Key.ToString(), g.Sum(a => a.CustoAquisicao.Centavos), g.Sum(a => AtivoDeCapitalQuant.ValorContabilAtualCentavos(a)),
                g.Count(a => a.Status == StatusAtivoDeCapital.Vendido), g.Sum(a => a.ResultadoAlienacaoCentavos ?? 0)))
            .OrderBy(c => c.Categoria)
            .ToList();

        var resultadoAlienacaoTotalCentavos = porCategoria.Sum(c => c.ResultadoAlienacaoCentavos);

        return new RoiInvestimento(
            capexCentavos, aportesCentavos, capexCentavos + aportesCentavos, giroConsumidoObservado,
            considerados.Count, porCategoria, resultadoAlienacaoTotalCentavos);
    }

    private static RoiRecuperacao CalcularRecuperacao(RoiInvestimento investimento, long fluxoOperacionalAcumulado)
    {
        var recuperado = investimento.AportesCentavos + fluxoOperacionalAcumulado;
        var faltam = Math.Max(0, investimento.TotalCentavos - recuperado);
        var percent = investimento.TotalCentavos == 0 ? 0m : Math.Round(100m * recuperado / investimento.TotalCentavos, 1);
        return new RoiRecuperacao(fluxoOperacionalAcumulado, recuperado, faltam, percent);
    }

    private static decimal ResolverTaxaMensal(int? taxaDescontoAnualBps)
    {
        if (taxaDescontoAnualBps is not { } bps || bps <= 0) return 0m;

        var taxaAnual = bps / 10_000m;
        return (decimal)Math.Pow((double)(1 + taxaAnual), 1.0 / 12) - 1m;
    }

    /// <summary>Projeção determinística (§7.7): margem = média de F_m das últimas 3 competências
    /// FECHADAS (meses anteriores ao mês corrente, dentro da série); parcelas EM ABERTO de contas
    /// ativo-de-capital, indexadas por mês de vencimento.</summary>
    private static (Dictionary<DateOnly, long> ParcelasAbertasPorMes, long MargemCaixaMensal) PrepararProjecao(
        IReadOnlyList<Domain.ContasAPagarReceber.ContaAPagar> contasAtivoDeCapital, IReadOnlyList<RoiSerieMensal> serie, DateOnly mesAtual)
    {
        var mesesFechados = serie.Where(s => s.Competencia < mesAtual).ToList();
        var ultimosTres = mesesFechados.Count > 3 ? mesesFechados.Skip(mesesFechados.Count - 3).ToList() : mesesFechados;
        var margemCaixaMensal = ultimosTres.Count == 0 ? 0L : (long)Math.Round(ultimosTres.Average(s => s.FluxoOperacionalCentavos), MidpointRounding.ToEven);

        var parcelasAbertasPorMes = new Dictionary<DateOnly, long>();
        foreach (var conta in contasAtivoDeCapital)
        {
            foreach (var parcela in conta.Parcelas)
            {
                if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;

                var mes = MesDe(DateOnly.FromDateTime(parcela.Vencimento.Date));
                var restante = (parcela.Valor - parcela.ValorPago).Centavos;
                parcelasAbertasPorMes[mes] = parcelasAbertasPorMes.GetValueOrDefault(mes, 0L) + restante;
            }
        }

        return (parcelasAbertasPorMes, margemCaixaMensal);
    }

    /// <summary>ROI% (§7.7): lente caixa (<c>Acum(T)/Investido</c>) e lente competência
    /// (<c>LucroOperAcum(T)/Investido</c> — <see cref="DreGerencialService"/>, MESMA fonte do DRE,
    /// nunca uma segunda conta de lucro). <c>MesesAteRoiCompleto</c> é o mesmo valor de
    /// <c>Payback.ProjetadoMeses</c> — campo de conveniência (§7.7).</summary>
    private async Task<RoiPercentuais> CalcularRoiPercentuaisAsync(
        string businessId, DateOnly m0, DateTimeOffset agora, long investidoCentavos, long acumuladoCentavos, int? mesesAteRoiCompleto, CancellationToken ct)
    {
        var caixaPercent = investidoCentavos == 0 ? 0m : Math.Round(100m * acumuladoCentavos / investidoCentavos, 1);

        var dreAcumulado = await dre.CalcularAsync(businessId, m0.InicioDoDia(), agora, ct).ConfigureAwait(false);
        var competenciaPercent = investidoCentavos == 0
            ? 0m
            : Math.Round(100m * dreAcumulado.ResultadoOperacional.Centavos / investidoCentavos, 1);

        return new RoiPercentuais(caixaPercent, competenciaPercent, mesesAteRoiCompleto);
    }
}
