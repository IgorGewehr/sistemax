using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Espelha <see cref="InMemoryAtivoDeCapitalRepository"/> — ver comentário lá sobre extensão para SQLite.</summary>
public sealed class InMemoryAporteDeCapitalRepository : IAporteDeCapitalRepository
{
    private readonly ConcurrentDictionary<string, AporteDeCapital> _porId = new();

    public Task<AporteDeCapital?> ObterPorIdAsync(string businessId, string aporteId, CancellationToken ct = default)
    {
        var aporte = _porId.GetValueOrDefault(aporteId);
        return Task.FromResult(aporte is not null && aporte.BusinessId == businessId ? aporte : null);
    }

    public Task<IReadOnlyList<AporteDeCapital>> ListarAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AporteDeCapital>>(_porId.Values
            .Where(a => a.BusinessId == businessId)
            .OrderBy(a => a.Data)
            .ToList());

    public Task SalvarAsync(AporteDeCapital aporte, CancellationToken ct = default)
    {
        _porId[aporte.Id] = aporte;
        return Task.CompletedTask;
    }

    public Task<bool> ExcluirAsync(string businessId, string aporteId, CancellationToken ct = default)
    {
        if (_porId.TryGetValue(aporteId, out var aporte) && aporte.BusinessId == businessId)
        {
            return Task.FromResult(_porId.TryRemove(aporteId, out _));
        }
        return Task.FromResult(false);
    }
}
