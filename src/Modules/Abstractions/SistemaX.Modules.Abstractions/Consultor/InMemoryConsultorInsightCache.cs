using System.Collections.Concurrent;

namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Implementação in-memory de <see cref="IConsultorInsightCache"/> — suficiente enquanto o
/// narrador é <see cref="NarradorTemplate"/> (grátis, então o cache aqui só prova o mecanismo;
/// o ganho real de custo aparece quando um <c>NarradorLLM</c> substituir o template). Registrada
/// como singleton pelo host (o dicionário precisa sobreviver entre requisições, senão "cache"
/// vira sempre miss).
/// </summary>
public sealed class InMemoryConsultorInsightCache : IConsultorInsightCache
{
    private sealed record Entrada(string FactsHash, ConsultorInsightNarrado Insight);

    private readonly ConcurrentDictionary<string, Entrada> _porChave = new();

    public Task<ConsultorInsightNarrado?> ObterSeAtualAsync(
        string businessId, string modulo, string ruleId, string factsHash, CancellationToken ct = default)
    {
        if (_porChave.TryGetValue(Chave(businessId, modulo, ruleId), out var entrada) && entrada.FactsHash == factsHash)
        {
            return Task.FromResult<ConsultorInsightNarrado?>(entrada.Insight);
        }

        return Task.FromResult<ConsultorInsightNarrado?>(null);
    }

    public Task GravarAsync(
        string businessId, string modulo, string ruleId, string factsHash, ConsultorInsightNarrado insight,
        CancellationToken ct = default)
    {
        _porChave[Chave(businessId, modulo, ruleId)] = new Entrada(factsHash, insight);
        return Task.CompletedTask;
    }

    private static string Chave(string businessId, string modulo, string ruleId) => $"{businessId}:{modulo}:{ruleId}";
}
