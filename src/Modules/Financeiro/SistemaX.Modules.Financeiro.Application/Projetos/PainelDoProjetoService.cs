using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
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

/// <summary>MC1 (margem de contribuição VARIÁVEL) do mês corrente — receita reconhecida do projeto
/// menos custo direto tageado do projeto, ambos filtrados por <c>ProjetoId</c> na competência do
/// mês corrente. Fonte idêntica à do DRE (<c>ContaAReceber</c>/<c>ContaAPagar</c> por competência +
/// <c>ReceitaReconhecidaResolver</c>) — nunca uma segunda fórmula. MC2 (amortização)/MC3 (tempo)
/// ficam para a Parte B (ativo amortizável / apontamento de tempo).</summary>
public sealed record PainelMargemProjeto(DateOnly Competencia, Money Receita, Money CustoDireto, Money Mc1, decimal Mc1Percent);

public sealed record PainelDoProjetoResultado(
    ProjetoDto Projeto, PainelReceitaProjeto Receita, PainelChurnProjeto Churn, PainelLtvProjeto Ltv, PainelMargemProjeto Margem);

/// <summary>
/// PAINEL DO PROJETO v1 (docs/financeiro/design-analise-por-projeto.md §9, Parte A/P2 do plano de
/// implementação): MRR, churn (hazard), LTV e margem de contribuição (MC1) de UM projeto,
/// <c>GET /financeiro/projetos/{id}/painel</c>. Toda métrica é DERIVADA — nada persistido, nada
/// cacheado — dos mesmos repositórios que o resto do módulo já usa, filtrados por
/// <c>ProjetoId</c>. Ativo amortizável (MC2), apontamento de tempo (MC3), payback, ROI e
/// capacidade ficam para a Parte B — ver design §9.1 para o shape completo hoje só parcialmente
/// implementado.
/// </summary>
public sealed class PainelDoProjetoService(
    IProjetoRepository projetos, IAssinaturaRepository assinaturas,
    IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar, IRelogio relogio)
{
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
        var margemMes = await CalcularMargemAsync(businessId, projetoId, competenciaMes, inicioMes, fimMes, ct).ConfigureAwait(false);

        // Piso do LTV (design §9.4): margem acumulada desde a CRIAÇÃO do projeto até agora — não
        // é "o mês", é o histórico inteiro, insumo só do piso realizado quando λ=0.
        var margemAcumulada = await CalcularMargemAsync(businessId, projetoId, competenciaMes, projeto.CriadoEm, agora, ct).ConfigureAwait(false);

        var ltv = CalcularLtv(margemMes, receita.AssinaturasAtivas, lambda, margemAcumulada.Mc1);

        return Result.Ok(new PainelDoProjetoResultado(ProjetoDto.DeDominio(projeto), receita, churn, ltv, margemMes));
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

    /// <summary>MC1 = ReceitaReconhecida do projeto na janela − CustoDireto tageado do projeto na
    /// janela (por <c>DataCompetencia</c>) — mesmas fontes/fórmula de <c>DreGerencialService</c>,
    /// só filtradas por <c>ProjetoId</c> em vez de agregadas pelo tenant inteiro. NÃO exclui
    /// categoria <c>ativo-diferido</c> (Parte A não tem <c>AtivoAmortizavel</c> ainda — Parte B).</summary>
    private async Task<PainelMargemProjeto> CalcularMargemAsync(
        string businessId, string projetoId, DateOnly competenciaLabel, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct)
    {
        var receitas = (await contasAReceber.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId && c.Status != StatusFinanceiro.Cancelado)
            .ToList();
        var receita = new Money(receitas.Sum(c => ReceitaReconhecidaResolver.CentavosNaJanela(c, inicio, fim)));

        var despesas = (await contasAPagar.ListarPorCompetenciaAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(c => c.ProjetoId == projetoId && c.Status != StatusFinanceiro.Cancelado)
            .ToList();
        var custoDireto = despesas.Aggregate(Money.Zero, (acc, c) => acc + c.ValorTotal);

        var mc1 = receita - custoDireto;
        var mc1Percent = receita.EhZero ? 0m : Math.Round((decimal)mc1.Centavos / receita.Centavos * 100m, 1);

        return new PainelMargemProjeto(competenciaLabel, receita, custoDireto, mc1, mc1Percent);
    }

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

    private static (DateTimeOffset Inicio, DateTimeOffset Fim) LimitesDoMes(DateTimeOffset agora)
    {
        var inicio = new DateTimeOffset(agora.Year, agora.Month, 1, 0, 0, 0, agora.Offset);
        var fim = inicio.AddMonths(1).AddTicks(-1);
        return (inicio, fim);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
