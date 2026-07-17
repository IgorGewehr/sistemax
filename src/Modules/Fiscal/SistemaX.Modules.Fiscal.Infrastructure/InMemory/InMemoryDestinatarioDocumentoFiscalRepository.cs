using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Gap #1 (emissao-mapping.md §4.2/§11) — sem vínculo, o mapper simplesmente omite o
/// bloco (nullable por natureza, NFC-e frequentemente não identifica o consumidor).</summary>
public sealed class InMemoryDestinatarioDocumentoFiscalRepository : IDestinatarioDocumentoFiscalRepository
{
    private readonly ConcurrentDictionary<string, DestinatarioDocumentoFiscal> _porDocumento = new();

    public Task<DestinatarioDocumentoFiscal?> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => Task.FromResult(_porDocumento.GetValueOrDefault(documentoFiscalId));

    public Task VincularAsync(string documentoFiscalId, DestinatarioDocumentoFiscal destinatario, CancellationToken ct = default)
    {
        _porDocumento[documentoFiscalId] = destinatario;
        return Task.CompletedTask;
    }
}
