using System.Text.Json;
using Microsoft.Data.Sqlite;
using SistemaX.Infrastructure.Local.Ids;
using SistemaX.Infrastructure.Local.Outbox;

namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <inheritdoc cref="ILocalUnitOfWork"/>
public sealed class LocalUnitOfWork : ILocalUnitOfWork
{
    private readonly IOutboxStore _outboxStore;
    private bool _finished;

    private LocalUnitOfWork(SqliteConnection connection, SqliteTransaction transaction, IOutboxStore outboxStore)
    {
        Connection = connection;
        Transaction = transaction;
        _outboxStore = outboxStore;
    }

    public static async Task<LocalUnitOfWork> BeginAsync(ILocalSqliteConnectionFactory connectionFactory, IOutboxStore outboxStore, CancellationToken ct)
    {
        var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        return new LocalUnitOfWork(connection, transaction, outboxStore);
    }

    public SqliteConnection Connection { get; }

    public SqliteTransaction Transaction { get; }

    public SqliteCommand CreateCommand()
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        return cmd;
    }

    public Task EnqueueOutboxAsync(string entityType, string entityId, OutboxOperation operation, object payload, CancellationToken ct = default)
    {
        var message = new OutboxMessage(
            Id: UlidGenerator.NewUlid(),
            EntityType: entityType,
            EntityId: entityId,
            Operation: operation,
            PayloadJson: JsonSerializer.Serialize(payload),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Status: OutboxStatus.Pending,
            Attempts: 0,
            NextAttemptAtUtc: null,
            LastError: null);

        return _outboxStore.EnqueueAsync(Connection, Transaction, message, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        ThrowIfFinished();
        await Transaction.CommitAsync(ct).ConfigureAwait(false);
        _finished = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_finished)
        {
            return;
        }

        await Transaction.RollbackAsync(ct).ConfigureAwait(false);
        _finished = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_finished)
        {
            // O chamador nunca commitou nem deu rollback explícito (ex.: exceção não tratada) —
            // desfaz por segurança. Nunca deixa uma transação pendurada.
            try
            {
                await Transaction.RollbackAsync().ConfigureAwait(false);
            }
            catch (SqliteException)
            {
                // Conexão já pode estar em estado inválido (ex.: erro fatal anterior) — ignorar
                // no caminho de dispose é o correto, o rollback automático do SQLite ao fechar
                // a conexão sem commit já garante que nada parcial fica persistido.
            }
        }

        await Transaction.DisposeAsync().ConfigureAwait(false);
        await Connection.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfFinished()
    {
        if (_finished)
        {
            throw new InvalidOperationException("Este Unit-of-Work já foi commitado ou desfeito — abra um novo via ILocalUnitOfWorkFactory.BeginAsync.");
        }
    }
}
