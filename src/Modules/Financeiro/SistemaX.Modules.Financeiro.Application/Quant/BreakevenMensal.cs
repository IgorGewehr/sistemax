namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Ponto de equilíbrio VIVO do mês (catálogo #7 do plano de inteligência do Financeiro —
/// docs/financeiro/inteligencia-arquitetura.md §4/ADR-0005) — "quanto preciso vender por dia?" e
/// "em que dia do mês eu já paguei as contas fixas?".
///
/// FÓRMULA CLÁSSICA de ponto de equilíbrio em R$: <c>ReceitaNecessária = CustosFixos ÷ MC%</c>
/// (MC% = margem de contribuição percentual, vinda de <c>fato_margem_produto</c>/#6). O que este
/// motor adiciona é o "dia vivo": em vez de comparar contra uma média, acumula a receita REAL dia a
/// dia (× MC%) até o acumulado bater os custos fixos — esse é o dia do breakeven de verdade, não
/// uma projeção teórica.
///
/// Quando o acumulado ainda não bateu nos dias já decorridos, projeta LINEARMENTE usando o RITMO
/// MÉDIO do próprio mês corrente (não EWMA — EWMA suaviza entre períodos; aqui queremos só "no
/// ritmo médio deste mês, quando bate"). Se a projeção ultrapassa os dias restantes do mês,
/// <see cref="Resultado.DiaDoEquilibrio"/> é <c>null</c> — "neste ritmo, não bate este mês".
/// </summary>
public static class BreakevenMensal
{
    public sealed record PontoReceitaDiaria(int DiaDoMes, long ReceitaCentavos);

    public sealed record Resultado(
        long ReceitaNecessariaMensalCentavos,
        long ReceitaNecessariaDiariaCentavos,
        long ReceitaAcumuladaCentavos,
        long MargemContribuicaoAcumuladaCentavos,
        int? DiaDoEquilibrio,
        bool JaAtingiuNoMes);

    public static Resultado Calcular(
        long custosFixosMensaisCentavos,
        double margemContribuicaoPercentual,
        IReadOnlyList<PontoReceitaDiaria> receitaDiariaDoMesOrdenadaPorDia,
        int diasNoMes)
    {
        if (diasNoMes <= 0) throw new ArgumentOutOfRangeException(nameof(diasNoMes), "Dias no mês deve ser positivo.");

        var mcPct = Math.Max(0, margemContribuicaoPercentual);
        var receitaNecessariaMensal = mcPct > 0
            ? (long)Math.Ceiling(custosFixosMensaisCentavos / mcPct)
            : long.MaxValue; // sem margem de contribuição, nenhuma receita "paga" os fixos
        var receitaNecessariaDiaria = mcPct > 0
            ? (long)Math.Ceiling((double)receitaNecessariaMensal / diasNoMes)
            : 0;

        long receitaAcumulada = 0;
        long mcAcumulada = 0;
        int? diaDoEquilibrio = null;

        foreach (var ponto in receitaDiariaDoMesOrdenadaPorDia)
        {
            receitaAcumulada += ponto.ReceitaCentavos;
            mcAcumulada += (long)Math.Round(ponto.ReceitaCentavos * mcPct, MidpointRounding.ToEven);

            if (diaDoEquilibrio is null && mcPct > 0 && mcAcumulada >= custosFixosMensaisCentavos)
            {
                diaDoEquilibrio = ponto.DiaDoMes;
            }
        }

        if (diaDoEquilibrio is null && mcPct > 0 && receitaDiariaDoMesOrdenadaPorDia.Count > 0)
        {
            var diasDecorridos = receitaDiariaDoMesOrdenadaPorDia.Count;
            var mediaDiariaDeMc = (double)mcAcumulada / diasDecorridos;

            if (mediaDiariaDeMc > 0)
            {
                var faltaCentavos = custosFixosMensaisCentavos - mcAcumulada;
                var diasAdicionaisNecessarios = (int)Math.Ceiling(faltaCentavos / mediaDiariaDeMc);
                var diaProjetado = diasDecorridos + diasAdicionaisNecessarios;
                if (diaProjetado <= diasNoMes) diaDoEquilibrio = diaProjetado;
            }
        }

        return new Resultado(
            receitaNecessariaMensal,
            receitaNecessariaDiaria,
            receitaAcumulada,
            mcAcumulada,
            diaDoEquilibrio,
            JaAtingiuNoMes: diaDoEquilibrio is not null && diaDoEquilibrio <= receitaDiariaDoMesOrdenadaPorDia.Count);
    }
}
