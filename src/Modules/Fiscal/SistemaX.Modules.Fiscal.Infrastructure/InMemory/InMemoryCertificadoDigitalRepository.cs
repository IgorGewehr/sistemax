using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Placeholder de dev/teste — certificado A1 nunca deveria viver em memória em produção
/// (gap #2, emissao-mapping.md §4.6/§11); um cofre real (Storage criptografado) substitui isto
/// quando a emissão de verdade for além do MOCK.</summary>
public sealed class InMemoryCertificadoDigitalRepository : ICertificadoDigitalRepository
{
    private readonly ConcurrentDictionary<string, CertificadoDigital> _porTenant = new();

    public Task<CertificadoDigital?> ObterAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult(_porTenant.GetValueOrDefault(tenantId));

    public Task SalvarAsync(string tenantId, CertificadoDigital certificado, CancellationToken ct = default)
    {
        _porTenant[tenantId] = certificado;
        return Task.CompletedTask;
    }
}
