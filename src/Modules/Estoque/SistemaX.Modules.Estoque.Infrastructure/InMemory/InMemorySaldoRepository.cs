using System.Collections.Concurrent;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Saldos;

namespace SistemaX.Modules.Estoque.Infrastructure.InMemory;

/// <summary>Adapter in-memory do read-model de saldo, chaveado por tenant+produto+depósito.</summary>
public sealed class InMemorySaldoRepository : ISaldoRepository
{
    private readonly ConcurrentDictionary<string, SaldoDeItem> _porChave = new();

    private static string Chave(string tenantId, string produtoId, string depositoId) => $"{tenantId}:{produtoId}:{depositoId}";

    public Task<SaldoDeItem?> ObterAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, produtoId, depositoId)));

    public Task<SaldoDeItem> ObterOuCriarAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => Task.FromResult(_porChave.GetValueOrDefault(Chave(tenantId, produtoId, depositoId)) ?? SaldoDeItem.Vazio(tenantId, produtoId, depositoId));

    public Task SalvarAsync(SaldoDeItem saldo, CancellationToken ct = default)
    {
        _porChave[Chave(saldo.TenantId, saldo.ProdutoId, saldo.DepositoId)] = saldo;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SaldoDeItem>> ListarAsync(string tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SaldoDeItem>>(_porChave.Values.Where(s => s.TenantId == tenantId).ToList());
}
