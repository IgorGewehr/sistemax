using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryTributacaoProdutoRepository : ITributacaoProdutoRepository
{
    private readonly ConcurrentDictionary<string, TributacaoProduto> _porChave = new();

    private static string Chave(string tenantId, string produtoId) => $"{tenantId}:{produtoId}";

    public Task<TributacaoProduto?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, produtoId)));

    public Task SalvarAsync(TributacaoProduto tributacao, CancellationToken ct = default)
    {
        _porChave[Chave(tributacao.TenantId, tributacao.ProdutoId)] = tributacao;
        return Task.CompletedTask;
    }
}
