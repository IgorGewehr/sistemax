namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// LAR ÚNICO da TIR (docs/financeiro/design-imobilizado-roi.md §7.6/§10) — bisseção sobre o VPL de
/// uma série de fluxos MENSAIS (índice 0 = m0, sem desconto; índice i = mês i, descontado
/// <c>(1+r)^-i</c>). Determinística, sem dependência nova, auditável contra <c>numpy.irr</c>/Excel.
/// Reservado para reuso pelo painel de projeto quando precisar de TIR (hoje só
/// <c>ReadModels.RoiDoNegocioService</c> chama).
/// </summary>
public static class TaxaInternaDeRetorno
{
    private const double LimiteInferior = -0.99;
    private const double LimiteSuperior = 10.0;
    private const double ToleranciaCentavos = 0.5;
    private const int MaximoDeIteracoes = 200;

    public sealed record Resultado(decimal? MensalPercent, decimal? AnualizadaPercent, string? MotivoIndefinida)
    {
        public static Resultado Indefinida(string motivo) => new(null, null, motivo);
    }

    /// <summary>
    /// Pré-condições de existência (§7.6, honestidade tipo LTV do design-pai §9.4): sem troca de
    /// sinal na série ⇒ <c>"sem-mudanca-de-sinal"</c> (negócio que nunca investiu ou nunca
    /// retornou não tem TIR); <c>VPL(lo)·VPL(hi) &gt; 0</c> no intervalo <c>(−99%, +1000%)</c> a.m.
    /// ⇒ <c>"sem-raiz-no-intervalo"</c>. Caso contrário, bisseciona até <c>|VPL| &lt; 0,5 centavo</c>
    /// ou <see cref="MaximoDeIteracoes"/> iterações (o intervalo já colapsa a ponto de máquina bem
    /// antes disso — 200 é folga, não o caminho comum).
    /// </summary>
    public static Resultado Calcular(IReadOnlyList<long> fluxosMensaisCentavos)
    {
        if (!fluxosMensaisCentavos.Any(f => f < 0) || !fluxosMensaisCentavos.Any(f => f > 0))
            return Resultado.Indefinida("sem-mudanca-de-sinal");

        double Vpl(double r)
        {
            double soma = 0;
            for (var i = 0; i < fluxosMensaisCentavos.Count; i++)
            {
                soma += fluxosMensaisCentavos[i] / Math.Pow(1 + r, i);
            }
            return soma;
        }

        var lo = LimiteInferior;
        var hi = LimiteSuperior;
        var vplLo = Vpl(lo);
        var vplHi = Vpl(hi);

        if (vplLo * vplHi > 0)
            return Resultado.Indefinida("sem-raiz-no-intervalo");

        var mid = 0.0;
        for (var iteracao = 0; iteracao < MaximoDeIteracoes; iteracao++)
        {
            mid = (lo + hi) / 2;
            var vplMid = Vpl(mid);
            if (Math.Abs(vplMid) < ToleranciaCentavos) break;

            if ((vplLo < 0) == (vplMid < 0))
            {
                lo = mid;
                vplLo = vplMid;
            }
            else
            {
                hi = mid;
            }
        }

        var mensal = Math.Round((decimal)mid * 100m, 2);
        var anualizada = Math.Round(((decimal)Math.Pow(1 + mid, 12) - 1) * 100m, 2);
        return new Resultado(mensal, anualizada, null);
    }
}
