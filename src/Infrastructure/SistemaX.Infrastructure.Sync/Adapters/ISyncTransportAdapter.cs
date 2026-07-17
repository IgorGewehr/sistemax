using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Adapters;

/// <summary>
/// Adapter de TRANSPORTE — como o lote sai daqui e chega no próximo salto. A implementação
/// default é HTTP (<see cref="HttpSyncTransportAdapter"/>); trocar de protocolo (ex.: gRPC) é
/// só trocar esta implementação, o resto do motor não muda.
/// </summary>
public interface ISyncTransportAdapter
{
    Task<PushBatchResult> PushBatchAsync(IReadOnlyList<OutboxMessage> batch, string terminalId, CancellationToken ct = default);

    Task<PullResult> PullAsync(SyncCursor cursor, string terminalId, int maxItems, CancellationToken ct = default);

    Task<bool> PingAsync(CancellationToken ct = default);
}
