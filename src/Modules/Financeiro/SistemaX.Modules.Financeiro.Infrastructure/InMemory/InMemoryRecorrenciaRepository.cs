using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryRecorrenciaRepository : IRecorrenciaRepository
{
    private readonly ConcurrentDictionary<string, RecorrenciaAgg> _porId = new();

    private static string Chave(string businessId, string id) => $"{businessId}:{id}";

    public Task<IReadOnlyList<RecorrenciaAgg>> ListarAtivasAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecorrenciaAgg>>(
            _porId.Values.Where(r => r.BusinessId == businessId && r.Ativa).ToList());

    public Task<RecorrenciaAgg?> BuscarAsync(string businessId, string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(Chave(businessId, id)));

    public Task SalvarAsync(RecorrenciaAgg recorrencia, CancellationToken ct = default)
    {
        _porId[Chave(recorrencia.BusinessId, recorrencia.Id)] = recorrencia;
        return Task.CompletedTask;
    }
}
