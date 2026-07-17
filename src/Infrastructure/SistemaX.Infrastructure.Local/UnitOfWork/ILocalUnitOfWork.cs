using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Outbox;

namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <summary>
/// A unidade de crash-safety é a transação do motor de banco local, NÃO a lógica da aplicação
/// (lição central do Supermarket-OS, ver docs/robustez §1: "Replaces 25+ sequential IPC
/// round-trips" — o antigo padrão de "criar venda" → "adicionar item" → "adicionar pagamento"
/// como chamadas separadas deixa estado órfão se qualquer uma falhar isoladamente).
///
/// Um <see cref="ILocalUnitOfWork"/> representa UM <c>BEGIN...COMMIT</c> SQLite via WAL: ou tudo
/// que for gravado através dele é persistido, ou nada é — mesmo que o processo morra no meio
/// (queda de luz, kill -9, BSOD). Ao reabrir, o SQLite descarta a transação incompleta
/// automaticamente (rollback do WAL); nunca existe "venda meio-finalizada".
///
/// Uso típico (fechamento de venda inteiro em UMA transação):
/// <code>
/// await using var uow = await factory.BeginAsync(ct);
/// var numero = await sequences.NextAsync(uow.Connection, uow.Transaction, "venda:pdv-01", ct);
/// // ... INSERT venda, INSERT itens em lote, INSERT pagamentos, UPDATE estoque ...
/// await uow.EnqueueOutboxAsync("Venda", vendaId, OutboxOperation.Insert, payload, ct);
/// await uow.CommitAsync(ct);
/// </code>
/// </summary>
public interface ILocalUnitOfWork : IAsyncDisposable
{
    SqliteConnection Connection { get; }

    SqliteTransaction Transaction { get; }

    /// <summary>Atalho para um <see cref="SqliteCommand"/> já vinculado a <see cref="Connection"/> e <see cref="Transaction"/>.</summary>
    SqliteCommand CreateCommand();

    /// <summary>
    /// Enfileira uma mensagem de sync usando a MESMA transação — nunca abre transação própria.
    /// É a materialização de código do princípio "impossível gravar sem enfileirar": todo
    /// caminho de escrita de negócio que usa este Unit-of-Work e chama este método antes do
    /// commit garante que o outbox e o dado de negócio vivem ou morrem juntos.
    /// </summary>
    Task EnqueueOutboxAsync(string entityType, string entityId, OutboxOperation operation, object payload, CancellationToken ct = default);

    /// <summary>Confirma a transação. Após chamado, o Unit-of-Work não pode mais ser usado.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Desfaz a transação explicitamente (também acontece implicitamente no Dispose se nunca commitada).</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
