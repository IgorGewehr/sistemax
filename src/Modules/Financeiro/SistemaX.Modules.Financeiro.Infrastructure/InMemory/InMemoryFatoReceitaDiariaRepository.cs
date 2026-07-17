using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFatoReceitaDiariaRepository : IFatoReceitaDiariaRepository
{
    private readonly ConcurrentDictionary<(string TenantId, DateOnly Dia, CorrenteDeReceita Corrente), long> _porChave = new();

    public Task AcumularAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, long deltaCentavos, CancellationToken ct = default)
    {
        _porChave.AddOrUpdate((tenantId, dia, corrente), deltaCentavos, (_, atual) => atual + deltaCentavos);
        return Task.CompletedTask;
    }

    public Task<FatoReceitaDiaria?> ObterAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, CancellationToken ct = default)
        => Task.FromResult(_porChave.TryGetValue((tenantId, dia, corrente), out var valor)
            ? new FatoReceitaDiaria(tenantId, dia, corrente, valor, DateTimeOffset.UtcNow)
            : null);

    public Task<IReadOnlyList<FatoReceitaDiaria>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoReceitaDiaria>>(_porChave
            .Where(kv => kv.Key.TenantId == tenantId && kv.Key.Dia >= de && kv.Key.Dia <= ate)
            .OrderBy(kv => kv.Key.Dia).ThenBy(kv => kv.Key.Corrente)
            .Select(kv => new FatoReceitaDiaria(tenantId, kv.Key.Dia, kv.Key.Corrente, kv.Value, DateTimeOffset.UtcNow))
            .ToList());

    public Task ZerarTudoAsync(CancellationToken ct = default)
    {
        _porChave.Clear();
        return Task.CompletedTask;
    }
}
