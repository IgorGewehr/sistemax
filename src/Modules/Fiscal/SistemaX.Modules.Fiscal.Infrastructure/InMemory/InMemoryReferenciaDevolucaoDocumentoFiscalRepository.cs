using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Gap #5 (emissao-mapping.md §4.6/§11) — sem vínculo, o mapper simplesmente omite o
/// bloco <c>referencias</c> (documento não é devolução, ou devolução ainda sem NF-e original
/// vinculada).</summary>
public sealed class InMemoryReferenciaDevolucaoDocumentoFiscalRepository : IReferenciaDevolucaoDocumentoFiscalRepository
{
    private readonly ConcurrentDictionary<string, string> _porDocumento = new();

    public Task<string?> ObterRefNFeAsync(string documentoFiscalId, CancellationToken ct = default)
        => Task.FromResult(_porDocumento.GetValueOrDefault(documentoFiscalId));

    public Task VincularAsync(string documentoFiscalId, string refNFe, CancellationToken ct = default)
    {
        _porDocumento[documentoFiscalId] = refNFe;
        return Task.CompletedTask;
    }
}
