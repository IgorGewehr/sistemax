using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Configuracao;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryConfiguracaoFinanceiraTenantRepository : IConfiguracaoFinanceiraTenantRepository
{
    private readonly ConcurrentDictionary<string, ConfiguracaoFinanceiraTenant> _porTenant = new();

    public Task<ConfiguracaoFinanceiraTenant?> ObterAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_porTenant.GetValueOrDefault(tenantId));

    public Task SalvarAsync(ConfiguracaoFinanceiraTenant configuracao, CancellationToken ct = default)
    {
        _porTenant[configuracao.TenantId] = configuracao;
        return Task.CompletedTask;
    }
}
