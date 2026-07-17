using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>
/// Adapter direto in-memory — suficiente para rodar o módulo e os testes sem infraestrutura
/// externa. EXTENSÍVEL PARA SQLITE: trocar o dicionário por um <c>DbContext</c>/Dapper mantendo
/// exatamente esta interface de port; nenhum código de Application/Domain muda. Thread-safe via
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> — suficiente para o caso de uso de um único
/// processo local (PDV/servidor de loja), não para concorrência distribuída multi-nó.
/// </summary>
public sealed class InMemoryContaAReceberRepository : IContaAReceberRepository
{
    private readonly ConcurrentDictionary<string, ContaAReceber> _porId = new();

    public Task<ContaAReceber?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<ContaAReceber?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(c => c.BusinessId == businessId && c.SourceRef.Chave == sourceRefChave));

    public Task SalvarAsync(ContaAReceber conta, CancellationToken ct = default)
    {
        _porId[conta.Id] = conta;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ContaAReceber>> ListarPorCompetenciaAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaAReceber>>(_porId.Values
            .Where(c => c.BusinessId == businessId && c.DataCompetencia >= inicio && c.DataCompetencia <= fim)
            .ToList());

    public Task<IReadOnlyList<ContaAReceber>> ListarAbertasAteAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ContaAReceber>>(_porId.Values
            .Where(c => c.BusinessId == businessId)
            .Where(c => c.Parcelas.Any(p => p.Status is Domain.Comum.StatusFinanceiro.Aberto or Domain.Comum.StatusFinanceiro.Parcial or Domain.Comum.StatusFinanceiro.Atrasado)
                        && c.Parcelas.Any(p => p.Vencimento <= referencia))
            .ToList());
}
