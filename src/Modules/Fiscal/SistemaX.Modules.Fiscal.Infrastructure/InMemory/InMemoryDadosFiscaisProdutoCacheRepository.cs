using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

public sealed class InMemoryDadosFiscaisProdutoCacheRepository : IDadosFiscaisProdutoCacheRepository
{
    private readonly ConcurrentDictionary<string, DadosFiscaisProdutoCache> _porChave = new();

    private static string Chave(string tenantId, string produtoId) => $"{tenantId}:{produtoId}";

    public Task<DadosFiscaisProdutoCache?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, produtoId)));

    public Task SalvarAsync(DadosFiscaisProdutoCache dados, CancellationToken ct = default)
    {
        _porChave[Chave(dados.TenantId, dados.ProdutoId)] = dados;
        return Task.CompletedTask;
    }
}
