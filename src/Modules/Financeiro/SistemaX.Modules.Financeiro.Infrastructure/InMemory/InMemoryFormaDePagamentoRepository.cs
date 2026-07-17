using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFormaDePagamentoRepository : IFormaDePagamentoRepository
{
    private readonly ConcurrentDictionary<string, FormaDePagamento> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<FormaDePagamento?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(Chave(businessId, id)));

    public Task<FormaDePagamento?> ObterPorNomeAsync(string businessId, string nome, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(f =>
            f.BusinessId == businessId && string.Equals(f.Nome.Trim(), nome.Trim(), StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<FormaDePagamento>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FormaDePagamento>>(
            _porId.Values.Where(f => f.BusinessId == businessId && (!apenasAtivas || f.Ativo)).ToList());

    public Task SalvarAsync(FormaDePagamento forma, CancellationToken ct = default)
    {
        _porId[Chave(forma.BusinessId, forma.Id)] = forma;
        return Task.CompletedTask;
    }
}
