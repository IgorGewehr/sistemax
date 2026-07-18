using System.Collections.Concurrent;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Domain.Fornecedores;

namespace SistemaX.Modules.Compras.Infrastructure.InMemory;

/// <summary>
/// Adapter in-memory — suficiente para rodar o módulo e os testes sem infraestrutura externa.
/// EXTENSÍVEL PARA SQLITE: trocar o dicionário por persistência real mantendo exatamente esta
/// interface de port; nenhum código de Application/Domain muda (mesmo padrão de
/// <c>InMemoryVendaRepository</c>).
/// </summary>
public sealed class InMemoryFornecedorRepository : IFornecedorRepository
{
    private readonly ConcurrentDictionary<string, Fornecedor> _porId = new();

    public Task<Fornecedor?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<Fornecedor?> ObterPorDocumentoAsync(string tenantId, string documento, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(f => f.TenantId == tenantId && f.Documento == documento));

    public Task SalvarAsync(Fornecedor fornecedor, CancellationToken ct = default)
    {
        _porId[fornecedor.Id] = fornecedor;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Fornecedor>> ListarAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Fornecedor>>(_porId.Values
            .Where(f => f.TenantId == tenantId)
            .OrderBy(f => f.RazaoSocial, StringComparer.OrdinalIgnoreCase)
            .ToList());
}
