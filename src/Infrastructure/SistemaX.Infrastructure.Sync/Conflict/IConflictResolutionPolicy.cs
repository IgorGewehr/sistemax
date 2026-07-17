namespace SistemaX.Infrastructure.Sync.Conflict;

/// <summary>Mapeia um tipo de entidade (string estável, ex.: "Venda", "Produto", "Estoque") para sua estratégia de conflito.</summary>
public interface IConflictResolutionPolicy
{
    ConflictStrategy StrategyFor(string entityType);
}
