using SistemaX.Infrastructure.Local.Outbox;

namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <inheritdoc cref="ILocalUnitOfWorkFactory"/>
public sealed class LocalUnitOfWorkFactory(ILocalSqliteConnectionFactory connectionFactory, IOutboxStore outboxStore) : ILocalUnitOfWorkFactory
{
    public async Task<ILocalUnitOfWork> BeginAsync(CancellationToken ct = default)
        => await LocalUnitOfWork.BeginAsync(connectionFactory, outboxStore, ct).ConfigureAwait(false);
}
