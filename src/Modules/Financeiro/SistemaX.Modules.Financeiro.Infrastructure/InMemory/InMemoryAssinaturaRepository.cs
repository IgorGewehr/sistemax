using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter in-memory de <see cref="IAssinaturaRepository"/>. Trocável por SqliteAssinaturaRepository
/// sem tocar Domain/Application (mesmo port).</summary>
public sealed class InMemoryAssinaturaRepository : IAssinaturaRepository
{
    private readonly ConcurrentDictionary<string, Assinatura> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<IReadOnlyList<Assinatura>> ListarAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Assinatura>>(
            _porId.Values.Where(a => a.BusinessId == businessId).ToList());

    public Task<IReadOnlyList<Assinatura>> ListarAtivasAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Assinatura>>(
            _porId.Values.Where(a => a.BusinessId == businessId && a.Status == StatusAssinatura.Ativa).ToList());

    public Task<Assinatura?> BuscarAsync(string businessId, string assinaturaId, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(Chave(businessId, assinaturaId)));

    public Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default)
    {
        _porId[Chave(assinatura.BusinessId, assinatura.Id)] = assinatura;
        return Task.CompletedTask;
    }
}
