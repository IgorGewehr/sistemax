using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryPerfilFiscalNcmRepository : IPerfilFiscalNcmRepository
{
    private readonly ConcurrentDictionary<string, PerfilFiscalNCM> _porChave = new();

    private static string Chave(string tenantId, RegimeTributario regime, string ncm) => $"{tenantId}:{regime}:{ncm}";

    public Task<PerfilFiscalNCM?> ObterAsync(string tenantId, RegimeTributario regime, string ncm, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, regime, ncm)));

    public Task SalvarAsync(PerfilFiscalNCM perfil, CancellationToken ct = default)
    {
        _porChave[Chave(perfil.TenantId, perfil.Regime, perfil.Ncm)] = perfil;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PerfilFiscalNCM>> ListarAsync(string tenantId, RegimeTributario regime, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PerfilFiscalNCM>>(
            _porChave.Values.Where(p => p.TenantId == tenantId && p.Regime == regime).ToList());
}
