using System.Collections.Concurrent;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Notas;

namespace SistemaX.Modules.Compras.Infrastructure.InMemory;

public sealed class InMemoryNotaDeCompraRepository : INotaDeCompraRepository
{
    private readonly ConcurrentDictionary<string, NotaDeCompra> _porId = new();

    public Task<NotaDeCompra?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<NotaDeCompra?> ObterPorChaveDeAcessoAsync(string tenantId, string chaveDeAcesso, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(n => n.TenantId == tenantId && n.ChaveDeAcesso?.Valor == chaveDeAcesso));

    public Task SalvarAsync(NotaDeCompra nota, CancellationToken ct = default)
    {
        _porId[nota.Id] = nota;
        return Task.CompletedTask;
    }
}
