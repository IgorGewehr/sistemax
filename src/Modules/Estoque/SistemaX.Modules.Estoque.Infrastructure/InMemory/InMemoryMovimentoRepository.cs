using System.Collections.Concurrent;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Razao;

namespace SistemaX.Modules.Estoque.Infrastructure.InMemory;

/// <summary>Adapter in-memory do razão. APPEND-ONLY por construção: não existe método de
/// remover/atualizar — só <see cref="SalvarAsync"/> (que sempre insere, nunca sobrescreve o
/// mesmo Id, já que cada movimento nasce com um ULID novo).</summary>
public sealed class InMemoryMovimentoRepository : IMovimentoRepository
{
    private readonly ConcurrentDictionary<string, MovimentoDeEstoque> _porId = new();

    public Task<bool> ExisteComChaveAsync(string chaveIdempotencia, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.Any(m => m.ChaveIdempotencia == chaveIdempotencia));

    public Task SalvarAsync(MovimentoDeEstoque movimento, CancellationToken ct = default)
    {
        _porId[movimento.Id] = movimento;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorOrigemAsync(string tenantId, string sourceRefChave, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoDeEstoque>>(_porId.Values
            .Where(m => m.TenantId == tenantId && m.Origem.Chave == sourceRefChave)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList());

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorProdutoAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoDeEstoque>>(_porId.Values
            .Where(m => m.TenantId == tenantId && m.ProdutoId == produtoId && m.DepositoId == depositoId)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList());

    public Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorPeriodoAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MovimentoDeEstoque>>(_porId.Values
            .Where(m => m.TenantId == tenantId && m.OcorridoEm >= inicio && m.OcorridoEm <= fim)
            .OrderBy(m => m.Id, StringComparer.Ordinal)
            .ToList());
}
