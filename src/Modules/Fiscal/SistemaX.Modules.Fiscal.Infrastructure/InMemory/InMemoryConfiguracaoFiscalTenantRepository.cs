using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryConfiguracaoFiscalTenantRepository : IConfiguracaoFiscalTenantRepository
{
    private readonly ConcurrentDictionary<string, ConfiguracaoFiscalTenant> _porTenant = new();

    public Task<ConfiguracaoFiscalTenant?> ObterAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_porTenant.GetValueOrDefault(tenantId));

    public Task SalvarAsync(ConfiguracaoFiscalTenant configuracao, CancellationToken ct = default)
    {
        _porTenant[configuracao.TenantId] = configuracao;
        return Task.CompletedTask;
    }
}
