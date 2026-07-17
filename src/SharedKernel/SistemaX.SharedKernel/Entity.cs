namespace SistemaX.SharedKernel;

/// <summary>Entidade com identidade estável. Igualdade por Id, não por valor.</summary>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    public override bool Equals(object? obj)
        => obj is Entity<TId> outra && EqualityComparer<TId>.Default.Equals(Id, outra.Id);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// Raiz de agregado: fronteira de consistência transacional. Acumula eventos de domínio
/// que a camada de aplicação publica APÓS o commit (nunca no meio da transação).
/// IDs são ULID (ordenável, gerado no terminal, sem colisão entre PDVs) — ver docs.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent @event) => _domainEvents.Add(@event);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
