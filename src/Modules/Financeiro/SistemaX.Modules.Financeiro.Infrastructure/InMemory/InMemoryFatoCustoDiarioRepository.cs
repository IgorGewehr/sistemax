using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFatoCustoDiarioRepository : IFatoCustoDiarioRepository
{
    private readonly ConcurrentDictionary<(string TenantId, DateOnly Dia, CorrenteDeReceita Corrente, string ProjetoId), long> _porChave = new();

    public Task AcumularAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, long deltaCentavos, string projetoId = "", CancellationToken ct = default)
    {
        _porChave.AddOrUpdate((tenantId, dia, corrente, projetoId), deltaCentavos, (_, atual) => atual + deltaCentavos);
        return Task.CompletedTask;
    }

    public Task<FatoCustoDiario?> ObterAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, string projetoId = "", CancellationToken ct = default)
        => Task.FromResult(_porChave.TryGetValue((tenantId, dia, corrente, projetoId), out var valor)
            ? new FatoCustoDiario(tenantId, dia, corrente, projetoId, valor, DateTimeOffset.UtcNow)
            : null);

    public Task<IReadOnlyList<FatoCustoDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoCustoDiario>>(_porChave
            .Where(kv => kv.Key.TenantId == tenantId && kv.Key.Dia >= de && kv.Key.Dia <= ate)
            .OrderBy(kv => kv.Key.Dia).ThenBy(kv => kv.Key.Corrente).ThenBy(kv => kv.Key.ProjetoId)
            .Select(kv => new FatoCustoDiario(tenantId, kv.Key.Dia, kv.Key.Corrente, kv.Key.ProjetoId, kv.Value, DateTimeOffset.UtcNow))
            .ToList());

    public Task ZerarTudoAsync(CancellationToken ct = default)
    {
        _porChave.Clear();
        return Task.CompletedTask;
    }
}
