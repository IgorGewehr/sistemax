namespace SistemaX.Infrastructure.Sync.Conflict;

/// <summary>Fachada fina sobre a política registrada — ponto único que o motor de sync consulta.</summary>
public sealed class ConflictResolver(IConflictResolutionPolicy policy)
{
    public ConflictStrategy StrategyFor(string entityType) => policy.StrategyFor(entityType);
}
