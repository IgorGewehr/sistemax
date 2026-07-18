using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IAtivoDeCapitalRepository"/> — mesmo molde de
/// <see cref="InMemoryProjetoRepository"/>.</summary>
public sealed class InMemoryAtivoDeCapitalRepository : IAtivoDeCapitalRepository
{
    private readonly ConcurrentDictionary<string, AtivoDeCapital> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<AtivoDeCapital?> ObterPorIdAsync(string businessId, string ativoId, CancellationToken ct = default)
    {
        var ativo = _porId.GetValueOrDefault(Chave(businessId, ativoId));
        return Task.FromResult(ativo is not null && ativo.BusinessId == businessId ? ativo : null);
    }

    public Task<IReadOnlyList<AtivoDeCapital>> ListarAsync(string businessId, string? projetoId = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AtivoDeCapital>>(
            _porId.Values
                .Where(a => a.BusinessId == businessId && (projetoId is null || a.ProjetoId == projetoId))
                .OrderBy(a => a.CriadoEm)
                .ToList());

    public Task<IReadOnlyList<AtivoDeCapital>> ListarEmUsoAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AtivoDeCapital>>(
            _porId.Values
                .Where(a => a.BusinessId == businessId && a.Status == StatusAtivoDeCapital.EmUso)
                .OrderBy(a => a.CriadoEm)
                .ToList());

    public Task SalvarAsync(AtivoDeCapital ativo, CancellationToken ct = default)
    {
        _porId[Chave(ativo.BusinessId, ativo.Id)] = ativo;
        return Task.CompletedTask;
    }
}
