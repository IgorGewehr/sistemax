using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local.Outbox;

/// <summary>
/// Acesso ao outbox transacional. <see cref="EnqueueAsync"/> é a metade "gravação" — sempre
/// chamada dentro de uma transação já aberta por quem está escrevendo o dado de negócio (ver
/// <see cref="SistemaX.Infrastructure.Local.UnitOfWork.ILocalUnitOfWork"/>). As demais operações
/// são a metade "leitura/drenagem", usadas pelo adapter de storage da camada de Sync
/// (<c>SistemaX.Infrastructure.Sync</c>, que referencia este projeto).
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Insere a mensagem usando a MESMA conexão/transação do chamador — nunca abre conexão
    /// própria aqui. É este acoplamento estrutural que torna "impossível" (no sentido de exigir
    /// um ato deliberado de pular a chamada) gravar dado de negócio sem enfileirar o evento de
    /// sync correspondente.
    /// </summary>
    Task EnqueueAsync(SqliteConnection connection, SqliteTransaction transaction, OutboxMessage message, CancellationToken ct = default);

    /// <summary>Lote de mensagens elegíveis para push: <c>Pending</c> e cuja janela de backoff já passou.</summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingBatchAsync(int maxBatchSize, CancellationToken ct = default);

    /// <summary>Marca como confirmadas (ACK recebido do próximo salto) — idempotente.</summary>
    Task MarkConfirmedAsync(IEnumerable<string> ids, CancellationToken ct = default);

    /// <summary>
    /// Registra uma tentativa falha: incrementa <c>Attempts</c>, grava o erro e agenda a
    /// próxima tentativa em <paramref name="nextAttemptDelay"/> (backoff calculado pelo
    /// chamador — ver <c>SistemaX.Infrastructure.Sync</c>). Não lança se o item não existir mais.
    /// </summary>
    Task MarkFailedAsync(string id, string error, TimeSpan nextAttemptDelay, CancellationToken ct = default);

    /// <summary>Move para dead-letter (excedeu maxRetries) — fica visível para retry/descarte manual auditado.</summary>
    Task MoveToDeadLetterAsync(string id, CancellationToken ct = default);

    /// <summary>Total de mensagens pendentes — usado para alertar "terminal com N mudanças pendentes há X tempo".</summary>
    Task<int> CountPendingAsync(CancellationToken ct = default);
}
