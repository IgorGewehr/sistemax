using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Gap #3 (emissao-mapping.md §4.5/§11) — nunca persistido no agregado fiscal; este
/// repositório é só o canal auxiliar entre quem sabe a forma de pagamento (Vendas/PDV) e o
/// mapper de emissão.</summary>
public sealed class InMemoryFormaPagamentoDocumentoFiscalRepository : IFormaPagamentoDocumentoFiscalRepository
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<FormaPagamentoParaEmitir>> _porDocumento = new();

    public Task<IReadOnlyList<FormaPagamentoParaEmitir>> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => Task.FromResult(_porDocumento.GetValueOrDefault(documentoFiscalId) ?? Array.Empty<FormaPagamentoParaEmitir>());

    public Task VincularAsync(string documentoFiscalId, IReadOnlyList<FormaPagamentoParaEmitir> pagamentos, CancellationToken ct = default)
    {
        _porDocumento[documentoFiscalId] = pagamentos;
        return Task.CompletedTask;
    }
}
