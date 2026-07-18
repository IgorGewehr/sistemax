using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IMovimentoMrrRepository"/>. Trocável por
/// <c>SqliteMovimentoMrrRepository</c> sem tocar Application/Domain (mesmo port).</summary>
public sealed class InMemoryMovimentoMrrRepository : IMovimentoMrrRepository
{
    private readonly ConcurrentBag<MovimentoMrr> _itens = [];

    public Task RegistrarAsync(MovimentoMrr movimento, CancellationToken ct = default)
    {
        _itens.Add(movimento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MovimentoMrr>> ListarAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoMrr>>(_itens.Where(m => m.BusinessId == businessId).ToList());
}
