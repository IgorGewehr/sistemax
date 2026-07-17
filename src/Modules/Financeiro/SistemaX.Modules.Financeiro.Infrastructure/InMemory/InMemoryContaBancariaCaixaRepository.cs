using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryContaBancariaCaixaRepository : IContaBancariaCaixaRepository
{
    private readonly ConcurrentDictionary<string, ContaBancariaCaixa> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<ContaBancariaCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(Chave(businessId, id)));

    public Task<IReadOnlyList<ContaBancariaCaixa>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaBancariaCaixa>>(
            _porId.Values.Where(c => c.BusinessId == businessId && (!apenasAtivas || c.Ativa)).ToList());

    public Task SalvarAsync(ContaBancariaCaixa conta, CancellationToken ct = default)
    {
        _porId[Chave(conta.BusinessId, conta.Id)] = conta;
        return Task.CompletedTask;
    }
}
