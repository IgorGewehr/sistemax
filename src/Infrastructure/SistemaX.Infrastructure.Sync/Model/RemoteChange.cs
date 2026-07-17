namespace SistemaX.Infrastructure.Sync.Model;

/// <summary>
/// Uma mudança já aceita pelo receptor de um salto (server_sequence atribuído) — é isto que
/// trafega no PULL para os demais terminais/hops. <see cref="Id"/> é o MESMO ULID gerado no
/// outbox de origem (ver <c>SistemaX.Infrastructure.Local.Outbox.OutboxMessage</c>) — a chave de
/// idempotência sobrevive ponta a ponta, do terminal de origem até quem aplica a mudança.
/// </summary>
public sealed record RemoteChange(
    string Id,
    string EntityType,
    string EntityId,
    string Operation,
    string PayloadJson,
    string OriginTerminalId,
    long ServerSequence,
    DateTimeOffset OccurredAtUtc);

/// <summary>Uma mudança recebida, ainda sem <c>ServerSequence</c> (atribuído pelo changelog ao gravar).</summary>
public sealed record IncomingChange(
    string Id,
    string EntityType,
    string EntityId,
    string Operation,
    string PayloadJson,
    string OriginTerminalId,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Cursor de pull: um número de sequência atribuído pelo RECEPTOR (nunca timestamp do cliente).
/// Mais robusto até que "timestamp monotônico do servidor" (a recomendação original do
/// Supermarket-OS, docs/robustez §3): um contador de sequência não sofre de nenhum risco
/// residual de resolução/skew de relógio, mesmo do lado do servidor.
/// </summary>
public sealed record SyncCursor(long ServerSequence);
