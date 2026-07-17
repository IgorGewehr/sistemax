using System.Collections.Concurrent;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Vinculos;

namespace SistemaX.Modules.Compras.Infrastructure.InMemory;

/// <summary>Chave de armazenamento composta (tenant+fornecedor+cProd, não o Id do agregado) — é
/// isso que garante o "único por (TenantId, FornecedorId, CProd)" (plano §3.1) e faz
/// <c>AtualizarMatch</c> sobrescrever o mesmo registro em vez de duplicar.</summary>
public sealed class InMemoryVinculoProdutoFornecedorRepository : IVinculoProdutoFornecedorRepository
{
    private readonly ConcurrentDictionary<string, VinculoProdutoFornecedor> _porChave = new();

    public Task<VinculoProdutoFornecedor?> ObterAsync(
        string tenantId, string fornecedorId, string codigoProdutoNoFornecedor, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, fornecedorId, codigoProdutoNoFornecedor)));

    public Task SalvarAsync(VinculoProdutoFornecedor vinculo, CancellationToken ct = default)
    {
        _porChave[Chave(vinculo.TenantId, vinculo.FornecedorId, vinculo.CodigoProdutoNoFornecedor)] = vinculo;
        return Task.CompletedTask;
    }

    private static string Chave(string tenantId, string fornecedorId, string cProd) => $"{tenantId}:{fornecedorId}:{cProd}";
}
