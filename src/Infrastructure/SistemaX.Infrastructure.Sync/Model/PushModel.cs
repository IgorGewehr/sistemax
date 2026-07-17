namespace SistemaX.Infrastructure.Sync.Model;

/// <summary>Resultado de aplicar (ou não) um item do lote, do ponto de vista de quem RECEBEU o push.</summary>
public enum PushItemOutcome
{
    /// <summary>Aplicada com sucesso agora.</summary>
    Confirmed,

    /// <summary>
    /// Idempotência em ação: este <c>Id</c> já tinha sido processado antes — resposta segura ao
    /// reenvio depois de uma falha de rede no meio de um push anterior (o cliente nunca sabe se
    /// o servidor recebeu ou não, então reenviar tem que ser seguro por construção).
    /// </summary>
    AlreadySynced,

    /// <summary>Rejeitada (erro de validação/aplicação) — o cliente decide se e quando reenviar (com backoff).</summary>
    Rejected
}

public sealed record PushItemResult(string Id, PushItemOutcome Outcome, string? Detail);

/// <summary>
/// Resultado de um push do PONTO DE VISTA DO CLIENTE: <see cref="TransportOk"/> distingue "o
/// servidor respondeu" (mesmo que tenha rejeitado itens individualmente) de "nem consegui falar
/// com o servidor" (timeout/DNS/rede) — só o segundo caso é tratado como falha de transporte
/// pelo <see cref="Client.SyncEngine"/> (mantém TODO o lote pendente, sem consumir tentativas
/// individuais por item).
/// </summary>
public sealed record PushBatchResult(bool TransportOk, IReadOnlyList<PushItemResult> Items);

public sealed record PullResult(bool TransportOk, IReadOnlyList<RemoteChange> Changes, long NewServerSequence);

// ── Contratos de wire (JSON) — o MESMO tipo é usado pelo adapter de transporte (cliente) e por
// SyncInboundService (receptor), então o host que expõe o endpoint HTTP (Store.Server, Cloud.Api)
// só precisa desserializar o body na rota nisto e chamar o serviço — sem redefinir o contrato. ──

public sealed record SyncPushRequestItem(string Id, string EntityType, string EntityId, string Operation, string PayloadJson, long CreatedAtUtcMs);

public sealed record SyncPushRequest(string TerminalId, IReadOnlyList<SyncPushRequestItem> Items);

public sealed record SyncPushResponseItem(string Id, string Outcome, string? Detail);

public sealed record SyncPushResponse(IReadOnlyList<SyncPushResponseItem> Items);

public sealed record SyncPullResponseItem(
    string Id, string EntityType, string EntityId, string Operation, string PayloadJson,
    string OriginTerminalId, long ServerSequence, long OccurredAtUtcMs);

public sealed record SyncPullResponse(IReadOnlyList<SyncPullResponseItem> Items, long NewServerSequence);
