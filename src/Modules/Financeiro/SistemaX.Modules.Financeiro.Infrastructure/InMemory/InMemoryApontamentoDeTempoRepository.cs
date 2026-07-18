using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IApontamentoDeTempoRepository"/> — mesmo molde de
/// <see cref="InMemoryProjetoRepository"/>.</summary>
public sealed class InMemoryApontamentoDeTempoRepository : IApontamentoDeTempoRepository
{
    private readonly ConcurrentDictionary<string, ApontamentoDeTempo> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<ApontamentoDeTempo?> ObterPorIdAsync(string businessId, string apontamentoId, CancellationToken ct = default)
    {
        var apontamento = _porId.GetValueOrDefault(Chave(businessId, apontamentoId));
        return Task.FromResult(apontamento is not null && apontamento.BusinessId == businessId ? apontamento : null);
    }

    public Task<IReadOnlyList<ApontamentoDeTempo>> ListarAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, string? projetoId = null, string? clienteId = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApontamentoDeTempo>>(
            _porId.Values
                .Where(a => a.BusinessId == businessId && a.Data >= de && a.Data <= ate)
                .Where(a => projetoId is null || a.ProjetoId == projetoId)
                .Where(a => clienteId is null || a.ClienteId == clienteId)
                .OrderBy(a => a.Data)
                .ToList());

    public Task SalvarAsync(ApontamentoDeTempo apontamento, CancellationToken ct = default)
    {
        _porId[Chave(apontamento.BusinessId, apontamento.Id)] = apontamento;
        return Task.CompletedTask;
    }

    public Task<bool> ExcluirAsync(string businessId, string apontamentoId, CancellationToken ct = default)
        => Task.FromResult(_porId.TryGetValue(Chave(businessId, apontamentoId), out var apontamento) && apontamento.BusinessId == businessId
            && _porId.TryRemove(Chave(businessId, apontamentoId), out _));
}
