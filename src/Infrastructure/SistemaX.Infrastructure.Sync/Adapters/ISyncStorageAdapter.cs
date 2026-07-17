using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Adapters;

/// <summary>
/// Adapter de STORAGE do motor de sync — a fonte do que precisa ser empurrado (outbox local, já
/// implementado por <c>SistemaX.Infrastructure.Local</c>) e o cursor de onde parou o último pull.
/// Trocar de motor de persistência local (ex.: SQLite → outro embarcado) significa só trocar a
/// implementação desta interface.
/// </summary>
public interface ISyncStorageAdapter
{
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int maxBatchSize, CancellationToken ct = default);

    Task MarkConfirmedAsync(IEnumerable<string> ids, CancellationToken ct = default);

    Task MarkFailedAsync(string id, string error, TimeSpan nextAttemptDelay, CancellationToken ct = default);

    Task MoveToDeadLetterAsync(string id, CancellationToken ct = default);

    Task<int> CountPendingAsync(CancellationToken ct = default);

    Task<SyncCursor> GetCursorAsync(CancellationToken ct = default);

    Task SaveCursorAsync(SyncCursor cursor, CancellationToken ct = default);
}
