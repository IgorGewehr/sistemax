using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFatoCaixaDiarioRepository : IFatoCaixaDiarioRepository
{
    private sealed record Acumulado(long EntradasCentavos, long SaidasCentavos);

    private readonly ConcurrentDictionary<(string TenantId, DateOnly Dia), Acumulado> _porChave = new();

    public Task AcumularEntradaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default)
    {
        _porChave.AddOrUpdate(
            (tenantId, dia),
            new Acumulado(deltaCentavos, 0),
            (_, atual) => atual with { EntradasCentavos = atual.EntradasCentavos + deltaCentavos });
        return Task.CompletedTask;
    }

    public Task AcumularSaidaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default)
    {
        _porChave.AddOrUpdate(
            (tenantId, dia),
            new Acumulado(0, deltaCentavos),
            (_, atual) => atual with { SaidasCentavos = atual.SaidasCentavos + deltaCentavos });
        return Task.CompletedTask;
    }

    public Task<FatoCaixaDiario?> ObterAsync(string tenantId, DateOnly dia, CancellationToken ct = default)
        => Task.FromResult(_porChave.TryGetValue((tenantId, dia), out var valor)
            ? new FatoCaixaDiario(tenantId, dia, valor.EntradasCentavos, valor.SaidasCentavos, DateTimeOffset.UtcNow)
            : null);

    public Task<IReadOnlyList<FatoCaixaDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoCaixaDiario>>(_porChave
            .Where(kv => kv.Key.TenantId == tenantId && kv.Key.Dia >= de && kv.Key.Dia <= ate)
            .OrderBy(kv => kv.Key.Dia)
            .Select(kv => new FatoCaixaDiario(tenantId, kv.Key.Dia, kv.Value.EntradasCentavos, kv.Value.SaidasCentavos, DateTimeOffset.UtcNow))
            .ToList());

    public Task ZerarTudoAsync(CancellationToken ct = default)
    {
        _porChave.Clear();
        return Task.CompletedTask;
    }
}
