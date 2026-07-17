namespace SistemaX.Infrastructure.Sync.Idempotency;

/// <summary>
/// Dedupe do lado RECEPTOR de um salto: antes de aplicar um item recebido, checa se este
/// <c>Id</c> (ULID vindo do outbox de origem) já foi processado. Se sim, responde
/// "AlreadySynced" sem reaplicar — é isto que torna seguro o terminal reenviar o mesmo lote após
/// uma falha de rede no meio do envio anterior (docs/robustez §3).
/// </summary>
public interface IProcessedMessageStore
{
    Task<bool> WasProcessedAsync(string id, CancellationToken ct = default);

    Task MarkProcessedAsync(string id, string entityType, string entityId, CancellationToken ct = default);
}
