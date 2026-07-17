namespace SistemaX.SharedKernel;

/// <summary>
/// Evento de DOMÍNIO: um fato relevante que ocorreu DENTRO de um agregado, no mesmo processo.
/// Diferente de evento de INTEGRAÇÃO (cross-módulo) — ver SistemaX.Modules.Abstractions.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
