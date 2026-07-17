using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFatoRecebiveisRepository : IFatoRecebiveisRepository
{
    private readonly ConcurrentQueue<FatoRecebivel> _itens = new();

    public Task AdicionarAsync(FatoRecebivel item, CancellationToken ct = default)
    {
        _itens.Enqueue(item);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FatoRecebivel>> ListarPorVencimentoAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoRecebivel>>(_itens
            .Where(i => i.TenantId == tenantId && i.Vencimento >= de && i.Vencimento <= ate)
            .OrderBy(i => i.Vencimento)
            .ToList());

    public Task ZerarTudoAsync(CancellationToken ct = default)
    {
        _itens.Clear();
        return Task.CompletedTask;
    }
}
