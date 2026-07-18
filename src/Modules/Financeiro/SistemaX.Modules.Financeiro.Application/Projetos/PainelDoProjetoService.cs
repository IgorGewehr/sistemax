using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Projetos;

/// <summary>Receita/MRR do projeto — Σ <see cref="Assinatura.Mrr"/> das assinaturas ATIVAS/
/// INADIMPLENTES tageadas neste projeto (mesmo racional de <c>ReceitaRecorrenteService</c>).</summary>
public sealed record PainelReceitaProjeto(Money Mrr, Money Arr, int AssinaturasAtivas, Money TicketMedio);

/// <summary>Churn por HAZARD DE EXPOSIÇÃO (design §9.4) — correto para n pequeno, ao contrário do
/// snapshot algébrico de <c>ReceitaRecorrenteService</c>. <see cref="VidaEsperadaMeses"/> é
/// <c>null</c> quando <c>λ=0</c> (nenhum cancelamento na janela — sobrevida indefinida).</summary>
public sealed record PainelChurnProjeto(
    int Cancelamentos12m, decimal ExposicaoAssinaturaMeses12m, decimal ChurnMensalPercent, decimal? VidaEsperadaMeses);

/// <summary>LTV = MC1 por assinatura × vida esperada (1/λ) — <c>null</c> honesto quando λ=0 (design
/// §9.4): nenhum cancelamento ainda observado, LTV é matematicamente indefinido, nunca inventado.
/// <see cref="LimiteInferior"/> é o piso realizado (margem acumulada desde a criação do projeto,
/// dividida pelas assinaturas ativas) — "o LTV já é ≥ isso".</summary>
public sealed record PainelLtvProjeto(Money? Ltv, Money LimiteInferior, string Metodo, string? Observacao);

/// <summary>Margem de contribuição em 3 CAMADAS (design §9.3, nunca misturadas): MC1 (variável) =
/// receita − custo direto tageado (excl. <c>ativo-de-capital</c>, que é balanço); MC2 (cheia) = MC1
/// − amortização/depreciação do mês (Σ <see cref="AtivoDeCapitalQuant"/> dos ativos do projeto); MC3
/// (gerencial) = MC2 − custo de tempo do mês — <c>null</c> nesta fatia (P4: custo/hora não resolvido,
/// decisão do dono), nunca <c>0</c> disfarçado de "sem custo de tempo".</summary>
public sealed record PainelMargemProjeto(
    DateOnly Competencia, Money Receita, Money CustoDireto, Money Mc1, decimal Mc1Percent,
    Money AmortizacaoMes, Money Mc2, decimal Mc2Percent, Money? CustoTempoMes, Money? Mc3, decimal? Mc3Percent);

/// <summary>Capacidade/ociosidade das licenças (design §9.6, Decisão D2) — só populado quando o
/// projeto tem ao menos um <c>AtivoDeCapital</c> <see cref="StatusAtivoDeCapital.EmUso"/> com
/// <c>QuantidadeUnidades</c> &gt; 1 lógica (default 1 já produz utilização 100%/0% sem erro).
/// <see cref="CustoOciosidadeMesCentavos"/>: a amortização corre sobre o TOTAL independente da
/// utilização — "licença parada também queima dinheiro" é o insight, nunca abatido do custo real.</summary>
public sealed record PainelCapacidadeProjeto(int UnidadesTotais, int UnidadesUtilizadas, decimal UtilizacaoPercent, long CustoOciosidadeMesCentavos);

/// <summary>Payback (design §9.5) — as DUAS lentes lado a lado (Decisão D5: caixa é o número-
/// manchete, competência ao lado). <see cref="PaybackRealizadoEm"/> é <c>null</c> enquanto o fluxo
/// acumulado de <c>MovimentoFinanceiro</c> do projeto não cruzar de negativo para ≥0 (ou nunca ficou
/// negativo — caso "fluxo nunca-negativo" do design, ex.: 5 licenças vendidas). <see cref="PaybackProjetadoMeses"/>
/// é a simulação determinística mês a mês (nunca fórmula de bolso) — horizonte 120 meses,
/// <c>null</c> se não cruza.</summary>
public sealed record PainelPaybackProjeto(
    long InvestimentoTotalCentavos, long FluxoCaixaAcumuladoCentavos, DateOnly? PaybackRealizadoEm,
    int? PaybackProjetadoMeses, string Metodo);

/// <summary>ROI por competência (design §9.5) — <c>null</c> honesto quando o denominador é zero
/// (nenhum custo econômico ainda, ou nenhum investimento registrado).</summary>
public sealed record PainelRoiProjeto(decimal? RealizadoPercent, decimal? RoiSobreInvestimentoPercent, decimal? RunRateAnualizadoPercent);

/// <summary>Bloco "onde vai meu tempo" do painel (design §9.1/§9.7) — <see cref="CustoJanelaCentavos"/>
/// é sempre <c>null</c> nesta fatia (decisão do dono — P4).</summary>
public sealed record PainelTempoPorCliente(string ClienteId, string? ClienteNome, int Minutos, long? CustoCentavos);

public sealed record PainelTempoProjeto(int MinutosJanela, long? CustoJanelaCentavos, IReadOnlyList<PainelTempoPorCliente> PorCliente);

public sealed record PainelDoProjetoResultado(
    ProjetoDto Projeto, PainelReceitaProjeto Receita, PainelChurnProjeto Churn, PainelLtvProjeto Ltv,
    PainelMargemProjeto Margem, PainelCapacidadeProjeto Capacidade, PainelPaybackProjeto Payback,
    PainelRoiProjeto Roi, PainelTempoProjeto Tempo);

/// <summary>
/// PAINEL DO PROJETO (docs/financeiro/design-analise-por-projeto.md §9): MRR, churn (hazard), LTV,
/// margem de contribuição em 3 camadas, capacidade/ociosidade, payback (realizado + projetado) e ROI
/// de UM projeto — <c>GET /financeiro/projetos/{id}/painel</c>. Toda métrica é DERIVADA — nada
/// persistido, nada cacheado — dos mesmos repositórios que o resto do módulo já usa, filtrados por
/// <c>ProjetoId</c>. Parte B (P3/P4) completa o shape que a Parte A deixou parcial: MC2/MC3, payback,
/// ROI, capacidade e o bloco de tempo.
/// </summary>
public sealed class PainelDoProjetoService(
    IProjetoRepository projetos, IAssinaturaRepository assinaturas,
    IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar,
    IAtivoDeCapitalRepository ativosDeCapital, IMovimentoFinanceiroRepository movimentos,
    IApontamentoDeTempoRepository apontamentos, IRelogio relogio)
{
    private const int HorizontePaybackMeses = 120;

    public async Task<Result<PainelDoProjetoResultado>> CalcularAsync(string businessId, string projetoId, CancellationToken ct = default)
    {
        var projeto = await projetos.ObterPorIdAsync(businessId, projetoId, ct).ConfigureAwait(false);
        if (projeto is null)
            return Result.Falhar<PainelDoProjetoResultado>(new Error("financeiro.projeto.nao_encontrado", $"Projeto '{projetoId}' não encontrado."));

        var agora = relogio.Agora();
        var assinaturasDoProjeto = (await assinaturas.ListarAsync(businessId, ct).ConfigureAwait(false))
            .Where(a => a.ProjetoId == projetoId)
            .ToList();

        var receita = CalcularReceita(assinaturasDoProjeto);
        var churn = CalcularChurn(assinaturasDoProjeto, projeto.CriadoEm, agora, out var lambda);

        var competenciaMes = new DateOnly(agora.Year, agora.Month, 1);
        var (inicioMes, fimMes) = LimitesDoMes(agora);

        var ativosDoProjeto = (await ativosDeCapital.ListarAsync(businessId, projetoId, ct).ConfigureAwait(false)).ToList();

        var margemMes = await CalcularMargemAsync(businessId, projetoId, competenciaMes, inicioMes, fimMes, ativosDoProjeto, ct).ConfigureAwait(false);

        // Piso do LTV (design §9.4): margem acumulada desde a CRIAÇÃO do projeto até agora — não
        // é "o mês", é o histórico inteiro, insumo só do piso realizado quando λ=0.
        var margemAcumulada = await CalcularMargemAsync(businessId, projetoId, competenciaMes, projeto.CriadoEm, agora, ativosDoProjeto, ct).ConfigureAwait(false);

        var ltv = CalcularLtv(margemMes, receita.AssinaturasAtivas, lambda, margemAcumulada.Mc1);

        var capacidade = CalcularCapacidade(ativosDoProjeto, receita.AssinaturasAtivas, margemMes.AmortizacaoMes.Centavos);

        var payback = await CalcularPaybackAsync(businessId, projetoId, ativosDoProjeto, receita.Mrr, agora, ct).ConfigureAwait(false);

        var roi = await CalcularRoiAsync(businessId, projetoId, projeto.CriadoEm, agora, ativosDoProjeto, margemMes, ct).ConfigureAwait(false);

        var tempo = await CalcularTempoAsync(businessId, projetoId, inicioMes, fimMes, ct).ConfigureAwait(false);

        return Result.Ok(new PainelDoProjetoResultado(ProjetoDto.DeDominio(projeto), receita, churn, ltv, margemMes, capacidade, payback, roi, tempo));
    }

    private static PainelReceitaProjeto CalcularReceita(IReadOnlyList<Assinatura> assinaturasDoProjeto)
    {
        var ativas = assinaturasDoProjeto.Where(a => a.Status is StatusAssinatura.Ativa or StatusAssinatura.Inadimplente).ToList();
        var mrr = ativas.Aggregate(Money.Zero, (acc, a) => acc + a.Mrr);
        var arr = mrr * 12;
        var ticket = ativas.Count == 0 ? Money.Zero : new Money(mrr.Centavos / ativas.Count);
        return new PainelReceitaProjeto(mrr, arr, ativas.Count, ticket);
    }

    /// <summary>Hazard por exposição (design §9.4): W = min(12, idade do projeto em meses);
    /// λ = cancelamentos no W / Σ assinatura-meses expostos no W (exposição fracionária por dias —
    /// nunca contagem inteira de meses, para não distorcer projetos jovens/pequenos).</summary>
    private static PainelChurnProjeto CalcularChurn(
        IReadOnlyList<Assinatura> assinaturasDoProjeto, DateTimeOffset projetoCriadoEm, DateTimeOffset agora, out decimal lambda)
    {
        var idadeMeses = Math.Max(1, (agora.Year - projetoCriadoEm.Year) * 12 + agora.Month - projetoCriadoEm.Month);
        var w = Math.Min(12, idadeMeses);
        var inicioJanela = agora.AddMonths(-w);

        var cancelamentos = assinaturasDoProjeto.Count(a =>
            a.Status == StatusAssinatura.Cancelada && a.CanceladaEm is { } c && c >= inicioJanela && c <= agora);

        var exposicaoMeses = 0m;
        foreach (var a in assinaturasDoProjeto)
        {
            var inicioExposicao = Max(a.DataInicio, inicioJanela);
            var fimExposicao = Min(a.CanceladaEm ?? agora, agora);
            if (fimExposicao <= inicioExposicao) continue;

            exposicaoMeses += (decimal)((fimExposicao - inicioExposicao).TotalDays / 30.0);
        }

        lambda = exposicaoMeses > 0 ? cancelamentos / exposicaoMeses : 0m;
        var churnPercent = Math.Round(lambda * 100m, 2);
        decimal? vidaEsperada = lambda > 0 ? Math.Round(1m / lambda, 2) : null;

        return new PainelChurnProjeto(cancelamentos, Math.Round(exposicaoMeses, 2), churnPercent, vidaEsperada);
    }

    /// <summary>MC1/MC2/MC3 do projeto na janela [inicio,fim] — mesmas fontes/fórmula de
    /// <c>DreGerencialService</c> (ContaAReceber/ContaAPagar por competência), acrescidas da
    /// amortização/depreciação dos ativos do projeto (<see cref="AtivoDeCapitalQuant"/>) e do custo
    /// de tempo (sempre <c>null</c> nesta fatia — P4).</summary>
    private async Task<PainelMargemProjeto> CalcularMargemAsync(
        string businessId, string projetoId, DateOnly competenciaLabel, DateTimeOffset inicio, DateTimeOffset fim,
        IReadOnlyList<AtivoDeCapital> ativosDoProjeto, CancellationToken ct)
    {
        var receitas = (await contasAReceber.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId && c.Status != StatusFinanceiro.Cancelado)
            .ToList();
        var receita = new Money(receitas.Sum(c => ReceitaReconhecidaResolver.CentavosNaJanela(c, inicio, fim)));

        var despesas = (await contasAPagar.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId && c.Status != StatusFinanceiro.Cancelado)
            .ToList();
        // MC1 exclui ativo-de-capital (design §9.3): compra de capacidade é balanço, não custo variável.
        var custoDireto = despesas
            .Where(c => c.CategoriaId != CategoriaFinanceiraPadrao.AtivoDeCapital)
            .Aggregate(Money.Zero, (acc, c) => acc + c.ValorTotal);

        var mc1 = receita - custoDireto;
        var mc1Percent = receita.EhZero ? 0m : Math.Round((decimal)mc1.Centavos / receita.Centavos * 100m, 1);

        var de = DateOnly.FromDateTime(inicio.Date);
        var ate = DateOnly.FromDateTime(fim.Date);
        var amortizacaoCentavos = ativosDoProjeto.Sum(a => AtivoDeCapitalQuant.SomaNaJanela(a, de, ate));
        var amortizacao = new Money(amortizacaoCentavos);

        var mc2 = mc1 - amortizacao;
        var mc2Percent = receita.EhZero ? 0m : Math.Round((decimal)mc2.Centavos / receita.Centavos * 100m, 1);

        // MC3/custo de tempo: sempre null nesta fatia (P4 — custo/hora não resolvido, decisão do
        // dono). O campo já nasce no shape para o painel não mudar de forma quando a valorização
        // em R$ chegar — nunca 0 disfarçado de "tempo grátis".
        Money? custoTempo = null;
        Money? mc3 = null;
        decimal? mc3Percent = null;

        return new PainelMargemProjeto(competenciaLabel, receita, custoDireto, mc1, mc1Percent, amortizacao, mc2, mc2Percent, custoTempo, mc3, mc3Percent);
    }

    /// <summary>Espelha <c>LancamentoContabilFactory.CategoriaAtivoDeCapital</c> — Application não
    /// referencia o tipo Domain só para essa constante aqui porque <c>CategoriaFinanceiraPadrao</c>
    /// (mesmo projeto) já a reexporta; usa-se a constante diretamente para deixar claro que é a
    /// MESMA categoria que exclui a despesa do DRE.</summary>

    /// <summary>LTV = MC1 mensal por assinatura × vida esperada (1/λ). λ=0 ⇒ <c>null</c> honesto +
    /// piso realizado (design §9.4) — nunca um número inventado a partir de zero cancelamentos.</summary>
    private static PainelLtvProjeto CalcularLtv(PainelMargemProjeto margemMes, int assinaturasAtivas, decimal lambda, Money mc1Acumulado)
    {
        var mc1PorAssinatura = assinaturasAtivas > 0 ? new Money(margemMes.Mc1.Centavos / assinaturasAtivas) : Money.Zero;
        var pisoRealizado = assinaturasAtivas > 0 ? new Money(mc1Acumulado.Centavos / assinaturasAtivas) : Money.Zero;

        if (lambda <= 0)
        {
            return new PainelLtvProjeto(
                null, pisoRealizado, "mcVariavel/churn",
                "churn=0 na janela — LTV indefinido; mostrado o piso realizado (margem acumulada desde a criação do projeto ÷ assinaturas ativas).");
        }

        var ltv = new Money((long)Math.Round(mc1PorAssinatura.Centavos / lambda, MidpointRounding.ToEven));
        return new PainelLtvProjeto(ltv, pisoRealizado, "mcVariavel/churn", null);
    }

    /// <summary>Capacidade/ociosidade (design §9.6, Decisão D2): unidadesTotais = Σ
    /// <c>QuantidadeUnidades</c> dos ativos EM USO do projeto; unidadesUtilizadas = assinaturas
    /// ativas (1 assinatura ↔ 1 unidade); ociosidade = amortização do mês × (1 − utilização).</summary>
    private static PainelCapacidadeProjeto CalcularCapacidade(IReadOnlyList<AtivoDeCapital> ativosDoProjeto, int assinaturasAtivas, long amortizacaoMesCentavos)
    {
        var unidadesTotais = ativosDoProjeto.Where(a => a.Status == StatusAtivoDeCapital.EmUso).Sum(a => a.QuantidadeUnidades);
        var unidadesUtilizadas = Math.Min(assinaturasAtivas, unidadesTotais);
        if (unidadesTotais == 0)
        {
            return new PainelCapacidadeProjeto(0, 0, 0m, 0);
        }

        var utilizacaoPercent = Math.Round(100m * unidadesUtilizadas / unidadesTotais, 1);
        var custoOciosidade = (long)Math.Round(amortizacaoMesCentavos * (1 - unidadesUtilizadas / (decimal)unidadesTotais), MidpointRounding.ToEven);
        return new PainelCapacidadeProjeto(unidadesTotais, unidadesUtilizadas, utilizacaoPercent, custoOciosidade);
    }

    /// <summary>Payback (design §9.5) — realizado (caixa, MovimentoFinanceiro do projeto) + projetado
    /// (simulação determinística mês a mês). Ver <see cref="PainelPaybackProjeto"/>.</summary>
    private async Task<PainelPaybackProjeto> CalcularPaybackAsync(
        string businessId, string projetoId, IReadOnlyList<AtivoDeCapital> ativosDoProjeto, Money mrr, DateTimeOffset agora, CancellationToken ct)
    {
        var investimentoTotal = ativosDoProjeto.Aggregate(0L, (acc, a) => acc + a.CustoAquisicao.Centavos);

        var inicioHistorico = ativosDoProjeto.Count > 0
            ? ativosDoProjeto.Min(a => new DateTimeOffset(a.DataAquisicao.Year, a.DataAquisicao.Month, a.DataAquisicao.Day, 0, 0, 0, TimeSpan.Zero))
            : agora.AddYears(-10);
        var movimentosDoProjeto = (await movimentos.ListarPorPeriodoAsync(businessId, inicioHistorico, agora, ct).ConfigureAwait(false))
            .Where(m => m.ProjetoId == projetoId)
            .OrderBy(m => m.DataMovimento)
            .ToList();

        var (fluxoAcumuladoHoje, paybackRealizadoEm) = CalcularFluxoRealizado(movimentosDoProjeto);

        var paybackProjetadoMeses = await CalcularPaybackProjetadoAsync(businessId, projetoId, mrr, fluxoAcumuladoHoje, agora, ct).ConfigureAwait(false);

        return new PainelPaybackProjeto(investimentoTotal, fluxoAcumuladoHoje, paybackRealizadoEm, paybackProjetadoMeses, "simulacao-fluxo-conhecido");
    }

    /// <summary>FluxoAcum(T) = Σ movimentos do projeto (+Entrada, −Saída) até T; payback realizado =
    /// primeiro T com FluxoAcum(T) ≥ 0 tendo existido FluxoAcum &lt; 0 antes (design §9.5).</summary>
    private static (long FluxoAcumulado, DateOnly? PaybackRealizadoEm) CalcularFluxoRealizado(IReadOnlyList<MovimentoFinanceiro> movimentosDoProjeto)
    {
        long acumulado = 0;
        var jaFicouNegativo = false;
        DateOnly? paybackEm = null;

        foreach (var m in movimentosDoProjeto)
        {
            if (acumulado < 0) jaFicouNegativo = true;

            acumulado += m.Tipo == TipoMovimentoFinanceiro.Entrada ? m.Valor.Centavos : -m.Valor.Centavos;

            if (paybackEm is null && jaFicouNegativo && acumulado >= 0)
            {
                paybackEm = DateOnly.FromDateTime(m.DataMovimento.Date);
            }
        }

        return (acumulado, paybackEm);
    }

    /// <summary>Simulação determinística mês a mês (design §9.5) — NUNCA fórmula de bolso.
    /// margemCaixaMensal = MRR − custo recorrente médio (últimas 3 competências fechadas, excl.
    /// ativo-de-capital); fluxo(m) = margemCaixaMensal − parcelas em aberto do projeto vencendo no
    /// mês m (inclui as parcelas restantes do investimento, automaticamente).</summary>
    private async Task<int?> CalcularPaybackProjetadoAsync(
        string businessId, string projetoId, Money mrr, long acumuladoHoje, DateTimeOffset agora, CancellationToken ct)
    {
        var custoRecorrenteMedio = await CalcularCustoRecorrenteMedioAsync(businessId, projetoId, agora, ct).ConfigureAwait(false);
        var margemCaixaMensal = mrr.Centavos - custoRecorrenteMedio;

        var parcelasAbertasPorMes = await ParcelasAbertasPorMesAsync(businessId, projetoId, agora, ct).ConfigureAwait(false);

        var acumulado = acumuladoHoje;
        for (var m = 1; m <= HorizontePaybackMeses; m++)
        {
            var chaveMes = new DateOnly(agora.Year, agora.Month, 1).AddMonths(m);
            var saidasDoMes = parcelasAbertasPorMes.GetValueOrDefault(chaveMes, 0L);
            acumulado += margemCaixaMensal - saidasDoMes;
            if (acumulado >= 0) return m;
        }
        return null;
    }

    private async Task<long> CalcularCustoRecorrenteMedioAsync(string businessId, string projetoId, DateTimeOffset agora, CancellationToken ct)
    {
        var inicioMesCorrente = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var inicioJanela = inicioMesCorrente.AddMonths(-3);
        var despesas = (await contasAPagar.ListarPorCompetenciaAsync(businessId, inicioJanela, inicioMesCorrente.AddTicks(-1), ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId && c.Status != StatusFinanceiro.Cancelado && c.CategoriaId != CategoriaFinanceiraPadrao.AtivoDeCapital)
            .ToList();
        var total = despesas.Sum(c => c.ValorTotal.Centavos);
        return total / 3;
    }

    private async Task<Dictionary<DateOnly, long>> ParcelasAbertasPorMesAsync(string businessId, string projetoId, DateTimeOffset agora, CancellationToken ct)
    {
        var horizonte = agora.AddMonths(HorizontePaybackMeses + 1);
        var contas = (await contasAPagar.ListarAbertasAteAsync(businessId, horizonte, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId)
            .ToList();

        var porMes = new Dictionary<DateOnly, long>();
        foreach (var conta in contas)
        {
            foreach (var parcela in conta.Parcelas)
            {
                if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;

                var mes = new DateOnly(parcela.Vencimento.Year, parcela.Vencimento.Month, 1);
                var restante = (parcela.Valor - parcela.ValorPago).Centavos;
                porMes[mes] = porMes.GetValueOrDefault(mes, 0L) + restante;
            }
        }
        return porMes;
    }

    /// <summary>ROI por competência (design §9.5). <c>CustoEconAcum</c> = custo direto acumulado
    /// (excl. ativo-de-capital) + amortização reconhecida acumulada, desde a criação do projeto.</summary>
    private async Task<PainelRoiProjeto> CalcularRoiAsync(
        string businessId, string projetoId, DateTimeOffset criadoEm, DateTimeOffset agora,
        IReadOnlyList<AtivoDeCapital> ativosDoProjeto, PainelMargemProjeto margemMes, CancellationToken ct)
    {
        var margemAcumulada = await CalcularMargemAsync(businessId, projetoId, new DateOnly(agora.Year, agora.Month, 1), criadoEm, agora, ativosDoProjeto, ct).ConfigureAwait(false);

        var receitaAcum = margemAcumulada.Receita.Centavos;
        var custoEconAcum = margemAcumulada.CustoDireto.Centavos + margemAcumulada.AmortizacaoMes.Centavos;

        decimal? realizadoPercent = custoEconAcum == 0 ? null : Math.Round(100m * (receitaAcum - custoEconAcum) / custoEconAcum, 1);

        var investimentoTotal = ativosDoProjeto.Aggregate(0L, (acc, a) => acc + a.CustoAquisicao.Centavos);
        decimal? roiSobreInvestimento = investimentoTotal == 0 ? null : Math.Round(100m * (receitaAcum - custoEconAcum) / investimentoTotal, 1);

        decimal? runRateAnualizado = margemMes.AmortizacaoMes.Centavos == 0
            ? null
            : Math.Round(100m * margemMes.Mc2.Centavos / margemMes.AmortizacaoMes.Centavos - 100m, 1);

        return new PainelRoiProjeto(realizadoPercent, roiSobreInvestimento, runRateAnualizado);
    }

    /// <summary>Bloco "onde vai meu tempo" (design §9.1/§9.7) — Σ minutos por cliente na janela do
    /// mês corrente. <c>CustoCentavos</c> sempre <c>null</c> nesta fatia (P4).</summary>
    private async Task<PainelTempoProjeto> CalcularTempoAsync(string businessId, string projetoId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var lista = await apontamentos.ListarAsync(businessId, inicio, fim, projetoId, ct: ct).ConfigureAwait(false);
        var minutosTotais = lista.Sum(a => a.Minutos);
        var custoTotal = lista.Any(a => a.CustoCentavos is null) ? (long?)null : lista.Sum(a => a.CustoCentavos!.Value);

        var porCliente = lista
            .Where(a => a.ClienteId is not null)
            .GroupBy(a => a.ClienteId!)
            .Select(g => new PainelTempoPorCliente(
                g.Key, g.First().ClienteNome, g.Sum(a => a.Minutos),
                g.Any(a => a.CustoCentavos is null) ? (long?)null : g.Sum(a => a.CustoCentavos!.Value)))
            .OrderByDescending(c => c.Minutos)
            .ToList();

        return new PainelTempoProjeto(minutosTotais, custoTotal, porCliente);
    }

    private static (DateTimeOffset Inicio, DateTimeOffset Fim) LimitesDoMes(DateTimeOffset agora)
    {
        var inicio = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var fim = inicio.AddMonths(1).AddTicks(-1);
        return (inicio, fim);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
