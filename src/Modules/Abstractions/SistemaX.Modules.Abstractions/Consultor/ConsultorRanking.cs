namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Ranking determinístico dos fatos que merecem virar card (ADR-0005 §3.5 passo 3: "Rank por
/// Score; garante ≥1 fato por tela com card; corta top-N"). Puramente ordinal — nenhuma
/// aleatoriedade, nenhuma chamada externa.
///
/// Regra: primeiro garante 1 fato (o de maior <see cref="ConsultorFato.Score"/>) por
/// <see cref="ConsultorFato.Tela"/> distinta — nenhuma tela com insight relevante fica sem card,
/// mesmo que seu score absoluto seja baixo perto de outra tela mais "dramática". Preenche o
/// restante do orçamento (<paramref name="topN"/>) pelos maiores scores globais. Empate de score é
/// resolvido por <see cref="ConsultorFato.RuleId"/> (ordinal) — desempate estável, sem depender de
/// ordem de inserção do provider.
/// </summary>
public static class ConsultorRanking
{
    public static IReadOnlyList<ConsultorFato> Selecionar(IReadOnlyList<ConsultorFato> fatos, int topN)
    {
        if (topN <= 0 || fatos.Count == 0) return [];

        var melhorPorTela = fatos
            .GroupBy(f => f.Tela)
            .Select(grupo => grupo
                .OrderByDescending(f => f.Score)
                .ThenBy(f => f.RuleId, StringComparer.Ordinal)
                .First())
            .OrderByDescending(f => f.Score)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ToList();

        var selecionados = melhorPorTela.Take(topN).ToList();
        if (selecionados.Count >= topN)
        {
            return OrdemFinal(selecionados);
        }

        var jaSelecionados = selecionados.Select(Chave).ToHashSet();
        var candidatosRestantes = fatos
            .Where(f => !jaSelecionados.Contains(Chave(f)))
            .OrderByDescending(f => f.Score)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal);

        foreach (var fato in candidatosRestantes)
        {
            if (selecionados.Count >= topN) break;
            selecionados.Add(fato);
        }

        return OrdemFinal(selecionados);
    }

    private static (string Modulo, string RuleId) Chave(ConsultorFato fato) => (fato.Modulo, fato.RuleId);

    private static IReadOnlyList<ConsultorFato> OrdemFinal(List<ConsultorFato> selecionados) => selecionados
        .OrderByDescending(f => f.Score)
        .ThenBy(f => f.RuleId, StringComparer.Ordinal)
        .ToList();
}
