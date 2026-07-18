using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryDocumentoFiscalRepository : IDocumentoFiscalRepository
{
    private readonly ConcurrentDictionary<string, DocumentoFiscal> _porId = new();

    public Task<DocumentoFiscal?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<DocumentoFiscal?> ObterPorOrigemAsync(string tenantId, string origemChave, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(d => d.TenantId == tenantId && d.Origem.Chave == origemChave));

    public Task SalvarAsync(DocumentoFiscal documento, CancellationToken ct = default)
    {
        _porId[documento.Id] = documento;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentoFiscal>> ListarNumeroAlocadoAntesDeAsync(string tenantId, DateTimeOffset antesDe, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentoFiscal>>(_porId.Values
            .Where(d => d.TenantId == tenantId && d.Status == StatusDocumentoFiscal.NumeroAlocado && d.CriadoEm < antesDe)
            .ToList());

    public Task<IReadOnlyList<DocumentoFiscal>> ListarAsync(string tenantId, StatusDocumentoFiscal? status = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DocumentoFiscal>>(_porId.Values
            .Where(d => d.TenantId == tenantId && (status is null || d.Status == status))
            .OrderByDescending(d => d.CriadoEm)
            .ToList());
}
