using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Seed manual via Settings — até o módulo dono do cadastro (Empresa/Tenant) existir e
/// publicar evento de integração (gap #4, emissao-mapping.md §3), mesmo papel de
/// <see cref="InMemoryPerfilFiscalNcmRepository"/> para NCM.</summary>
public sealed class InMemoryCadastroFiscalEmitenteRepository : ICadastroFiscalEmitenteRepository
{
    private readonly ConcurrentDictionary<string, CadastroFiscalEmitente> _porTenant = new();

    public Task<CadastroFiscalEmitente?> ObterAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_porTenant.GetValueOrDefault(tenantId));

    public Task SalvarAsync(CadastroFiscalEmitente cadastro, CancellationToken ct = default)
    {
        _porTenant[cadastro.TenantId] = cadastro;
        return Task.CompletedTask;
    }
}
