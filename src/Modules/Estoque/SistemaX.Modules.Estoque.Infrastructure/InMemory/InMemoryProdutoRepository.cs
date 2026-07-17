using System.Collections.Concurrent;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;

namespace SistemaX.Modules.Estoque.Infrastructure.InMemory;

/// <summary>Adapter direto in-memory — suficiente para rodar o módulo e os testes sem
/// infraestrutura externa. Extensível para SQLite mantendo exatamente este port (nenhum código de
/// Application/Domain muda).</summary>
public sealed class InMemoryProdutoRepository : IProdutoRepository
{
    private readonly ConcurrentDictionary<string, Produto> _porId = new();

    public Task<Produto?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<Produto?> ObterPorSkuAsync(string tenantId, string sku, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(p => p.TenantId == tenantId && p.Sku == sku));

    public Task SalvarAsync(Produto produto, CancellationToken ct = default)
    {
        _porId[produto.Id] = produto;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Produto>> ListarAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Produto>>(_porId.Values.Where(p => p.TenantId == tenantId).ToList());
}
