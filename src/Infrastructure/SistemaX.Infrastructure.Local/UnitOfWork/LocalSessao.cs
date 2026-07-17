using SistemaX.Infrastructure.Local.Outbox;

namespace SistemaX.Infrastructure.Local.UnitOfWork;

/// <inheritdoc cref="ILocalSessao"/>
/// <remarks>
/// Registrada SCOPED (uma por requisição HTTP/execução de caso de uso). Implementa
/// <see cref="IAsyncDisposable"/> por segurança: se o caso de uso nunca chamar
/// <see cref="CommitAsync"/> nem <see cref="RollbackAsync"/> (ex.: exceção não tratada), o
/// container encerra o escopo e dispara o dispose — que por sua vez desfaz a transação pendurada
/// (mesma garantia de <see cref="LocalUnitOfWork.DisposeAsync"/>, um nível acima).
/// </remarks>
public sealed class LocalSessao(ILocalUnitOfWorkFactory unitOfWorkFactory) : ILocalSessao, IAsyncDisposable
{
    private ILocalUnitOfWork? _atual;

    public ILocalUnitOfWork? Atual => _atual;

    public async Task<ILocalUnitOfWork> IniciarAsync(CancellationToken ct = default)
    {
        if (_atual is not null)
        {
            return _atual;
        }

        _atual = await unitOfWorkFactory.BeginAsync(ct).ConfigureAwait(false);
        return _atual;
    }

    public Task EnqueueOutboxAsync(string entityType, string entityId, OutboxOperation operation, object payload, CancellationToken ct = default)
        => RequireAtual().EnqueueOutboxAsync(entityType, entityId, operation, payload, ct);

    public Task CommitAsync(CancellationToken ct = default) => RequireAtual().CommitAsync(ct);

    public Task RollbackAsync(CancellationToken ct = default) => _atual is null ? Task.CompletedTask : _atual.RollbackAsync(ct);

    public ValueTask DisposeAsync() => _atual?.DisposeAsync() ?? ValueTask.CompletedTask;

    private ILocalUnitOfWork RequireAtual()
        => _atual ?? throw new InvalidOperationException(
            "Nenhuma sessão ativa — chame ILocalSessao.IniciarAsync() antes de operar dentro dela.");
}
