using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryCartaCorrecaoFiscalRepository : ICartaCorrecaoFiscalRepository
{
    private readonly ConcurrentDictionary<string, CartaCorrecaoFiscal> _porId = new();

    public Task<IReadOnlyList<CartaCorrecaoFiscal>> ListarPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CartaCorrecaoFiscal>>(_porId.Values
            .Where(c => c.DocumentoFiscalId == documentoFiscalId)
            .OrderBy(c => c.Sequencia)
            .ToList());

    public Task SalvarAsync(CartaCorrecaoFiscal carta, CancellationToken ct = default)
    {
        _porId[carta.Id] = carta;
        return Task.CompletedTask;
    }
}
