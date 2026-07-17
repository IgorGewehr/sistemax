namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Orquestrador module-agnostic do Super Consultor (ADR-0005 §3.5): coleta de TODOS os
/// <see cref="IConsultorFactProvider"/> registrados via DI (hoje só o do Financeiro; Estoque/
/// Vendas/Compras/Fiscal se plugam depois SEM tocar esta classe — R5), rankeia
/// (<see cref="ConsultorRanking"/>), narra só o que mudou desde a última vez
/// (<see cref="IConsultorInsightCache"/>) e devolve a lista final na MESMA ordem do ranking.
///
/// Nenhuma camada aqui chama LLM — <see cref="IConsultorNarrador"/> é injetado (hoje
/// <see cref="NarradorTemplate"/>, grátis e determinístico). Trocar para um narrador real é uma
/// mudança de registro de DI, não de código aqui.
/// </summary>
public sealed class ConsultorService(
    IEnumerable<IConsultorFactProvider> provedores,
    IConsultorNarrador narrador,
    IConsultorInsightCache cache)
{
    public const int TopNPadrao = 8;

    public async Task<IReadOnlyList<ConsultorInsightNarrado>> GerarInsightsAsync(
        PeriodoRef periodo, int topN = TopNPadrao, CancellationToken ct = default)
    {
        var todosOsFatos = new List<ConsultorFato>();
        foreach (var provedor in provedores)
        {
            var fatos = await provedor.ColetarAsync(periodo, ct).ConfigureAwait(false);
            todosOsFatos.AddRange(fatos);
        }

        var selecionados = ConsultorRanking.Selecionar(todosOsFatos, topN <= 0 ? TopNPadrao : topN);
        if (selecionados.Count == 0) return [];

        var hashPorFato = selecionados.ToDictionary(Chave, fato => ConsultorFatoHasher.Hash(fato.Facts));

        var resultado = new List<ConsultorInsightNarrado>(selecionados.Count);
        var paraNarrar = new List<ConsultorFato>();

        foreach (var fato in selecionados)
        {
            var hash = hashPorFato[Chave(fato)];
            var emCache = await cache
                .ObterSeAtualAsync(periodo.BusinessId, fato.Modulo, fato.RuleId, hash, ct)
                .ConfigureAwait(false);

            if (emCache is not null)
            {
                resultado.Add(emCache);
            }
            else
            {
                paraNarrar.Add(fato);
            }
        }

        if (paraNarrar.Count > 0)
        {
            var narrados = await narrador.NarrarAsync(paraNarrar, ct).ConfigureAwait(false);
            foreach (var insight in narrados)
            {
                var hash = hashPorFato[(insight.Modulo, insight.RuleId)];
                await cache
                    .GravarAsync(periodo.BusinessId, insight.Modulo, insight.RuleId, hash, insight, ct)
                    .ConfigureAwait(false);
                resultado.Add(insight);
            }
        }

        var ordemOriginal = selecionados
            .Select((fato, indice) => (Chave: Chave(fato), Ordem: indice))
            .ToDictionary(par => par.Chave, par => par.Ordem);

        return resultado
            .OrderBy(insight => ordemOriginal[(insight.Modulo, insight.RuleId)])
            .ToList();
    }

    private static (string Modulo, string RuleId) Chave(ConsultorFato fato) => (fato.Modulo, fato.RuleId);
}
