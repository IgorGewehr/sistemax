using System.Collections.Concurrent;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>Adapter direto in-memory — usado por padrão em teste (sem contexto SQLite). NÃO
/// representa a alocação atômica cross-processo real (isso é o papel do adapter Sqlite via
/// <c>ILocalSequenceAllocator</c>), mas é atômica dentro do próprio processo via lock — suficiente
/// para o caso de uso in-memory.</summary>
public sealed class InMemorySequenciaFiscalRepository : ISequenciaFiscalRepository
{
    private readonly ConcurrentDictionary<string, long> _proximoPorChave = new();

    public Task<Result<long>> AlocarProximoAsync(string tenantId, string modelo, string serie, CancellationToken ct = default)
    {
        var chave = $"{tenantId}:{modelo}:{serie}";
        var numero = _proximoPorChave.AddOrUpdate(chave, 1, static (_, atual) => atual + 1);
        return Task.FromResult(Result.Ok(numero));
    }
}
