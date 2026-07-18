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
///
/// ENRIQUECIMENTO — ideia 1 do matemonstro (docs/financeiro/ideias-matemonstro.md, `fin-gestao-03`):
/// três leituras DERIVADAS do mesmo cálculo, sem fact table/evento novo algum:
/// <list type="bullet">
/// <item><see cref="Resultado.MargemDeSegurancaPercentual"/> — <c>MS = (ReceitaAtual −
/// ReceitaNecessária) ÷ ReceitaAtual</c>: quanto a receita pode cair antes do prejuízo. Usa
/// <see cref="Resultado.ReceitaAcumuladaCentavos"/> como "receita atual" (já é a soma real do mês
/// até hoje — nenhum dado novo). <c>null</c> sem margem de contribuição medida ou sem receita
/// acumulada (dividir por zero não é "0%", é "sem dado").</item>
/// <item><see cref="Resultado.Gao"/> (grau de alavancagem operacional) — <c>GAO = MC ÷
/// Lucro_operacional</c>, com <c>MC = MargemContribuicaoAcumuladaCentavos</c> e
/// <c>Lucro_operacional = MC − CustosFixosMensais</c>: quanto o lucro oscila por 1% de variação na
/// receita — o número que discrimina o negócio de custo fixo alto (restaurante de aluguel caro,
/// salão com muitas cadeiras) do de custo fixo baixo. <c>null</c> quando
/// <c>Lucro_operacional ≤ 0</c> (alavancagem operacional não é definida em prejuízo — nunca divide
/// por zero/negativo).</item>
/// <item><see cref="Resultado.ReceitaNecessariaMensalEconomicaCentavos"/> (ponto de equilíbrio
/// ECONÔMICO) — <c>ReceitaNecessária_econ = (CustosFixos + CustoDeOportunidadeMensal) ÷ MC%</c>:
/// soma ao custo fixo o retorno mínimo que o dono exige do capital investido (mesma taxa de
/// desconto do painel de ROI/imobilizado) ANTES de dividir pela MC% — o "zero a zero" contábil não
/// paga o dono. <see cref="Calcular"/> recebe <c>custoDeOportunidadeMensalCentavos</c> já em
/// centavos/mês (0 por padrão — sem config de taxa de oportunidade cadastrada, degrada
/// EXATAMENTE para o PE contábil, invariante testado).</item>
/// </list>
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
        bool JaAtingiuNoMes,
        double? MargemDeSegurancaPercentual,
        double? Gao,
        long ReceitaNecessariaMensalEconomicaCentavos);

    public static Resultado Calcular(
        long custosFixosMensaisCentavos,
        double margemContribuicaoPercentual,
        IReadOnlyList<PontoReceitaDiaria> receitaDiariaDoMesOrdenadaPorDia,
        int diasNoMes,
        long custoDeOportunidadeMensalCentavos = 0)
    {
        if (diasNoMes <= 0) throw new ArgumentOutOfRangeException(nameof(diasNoMes), "Dias no mês deve ser positivo.");

        var mcPct = Math.Max(0, margemContribuicaoPercentual);
        var receitaNecessariaMensal = mcPct > 0
            ? (long)Math.Ceiling(custosFixosMensaisCentavos / mcPct)
            : long.MaxValue; // sem margem de contribuição, nenhuma receita "paga" os fixos
        var receitaNecessariaDiaria = mcPct > 0
            ? (long)Math.Ceiling((double)receitaNecessariaMensal / diasNoMes)
            : 0;
        var receitaNecessariaMensalEconomica = mcPct > 0
            ? (long)Math.Ceiling((custosFixosMensaisCentavos + custoDeOportunidadeMensalCentavos) / mcPct)
            : long.MaxValue;

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

        // MS: precisa de MC% medida e receita acumulada positiva (senão "quanto pode cair" não faz
        // sentido — 0/0 é "sem dado", não "0%").
        double? margemDeSeguranca = mcPct > 0 && receitaAcumulada > 0
            ? (double)(receitaAcumulada - receitaNecessariaMensal) / receitaAcumulada
            : null;

        // GAO: só definido em lucro operacional POSITIVO (mesma razão de MS ≤ 0 ser indefinida —
        // ver doc de classe).
        var lucroOperacionalAcumulado = mcAcumulada - custosFixosMensaisCentavos;
        double? gao = lucroOperacionalAcumulado > 0
            ? (double)mcAcumulada / lucroOperacionalAcumulado
            : null;

        return new Resultado(
            receitaNecessariaMensal,
            receitaNecessariaDiaria,
            receitaAcumulada,
            mcAcumulada,
            diaDoEquilibrio,
            JaAtingiuNoMes: diaDoEquilibrio is not null && diaDoEquilibrio <= receitaDiariaDoMesOrdenadaPorDia.Count,
            margemDeSeguranca,
            gao,
            receitaNecessariaMensalEconomica);
    }
}
