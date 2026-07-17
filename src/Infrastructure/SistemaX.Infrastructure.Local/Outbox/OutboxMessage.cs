namespace SistemaX.Infrastructure.Local.Outbox;

/// <summary>Operação de escrita que originou a mensagem de outbox.</summary>
public enum OutboxOperation
{
    Insert,
    Update,
    Delete
}

/// <summary>
/// Estado do ciclo de vida de uma mensagem no outbox local. <see cref="Pending"/> é o único
/// estado elegível para push; os demais são terminais ou aguardam nova janela de backoff.
/// </summary>
public enum OutboxStatus
{
    /// <summary>Aguardando envio (ou reenvio, se <see cref="OutboxMessage.NextAttemptAtUtc"/> já passou).</summary>
    Pending,

    /// <summary>Confirmada pelo próximo salto (ACK recebido) — fim de vida, mantida só para auditoria.</summary>
    Confirmed,

    /// <summary>
    /// Excedeu <c>maxRetries</c>. Diferente do Supermarket-OS (onde itens assim ficavam "mudos",
    /// ver docs/robustez §3 fraqueza 3), este estado é EXPLÍCITO e consultável — vira fila de
    /// purgatório visível ao admin, nunca some silenciosamente.
    /// </summary>
    DeadLetter
}

/// <summary>
/// Uma linha do outbox transacional: o "é impossível gravar sem enfileirar o sync" do projeto.
/// É gravada NA MESMA transação SQLite da escrita de negócio que a originou — nunca em uma
/// segunda transação separada (ver <see cref="SistemaX.Infrastructure.Local.UnitOfWork.ILocalUnitOfWork"/>).
/// </summary>
/// <param name="Id">
/// ULID gerado no terminal — a chave de idempotência ponta a ponta. NUNCA derivado de timestamp
/// de baixa resolução (a lição do bug do Supermarket-OS: chave com granularidade de 1 segundo
/// colidia em updates em lote e derrubava a transação inteira via UNIQUE INDEX). Ver
/// <see cref="Ids.UlidGenerator"/>.
/// </param>
public sealed record OutboxMessage(
    string Id,
    string EntityType,
    string EntityId,
    OutboxOperation Operation,
    string PayloadJson,
    DateTimeOffset CreatedAtUtc,
    OutboxStatus Status,
    int Attempts,
    DateTimeOffset? NextAttemptAtUtc,
    string? LastError);
