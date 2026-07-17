using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Contabil;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

/// <summary>Adapter direto do motor invisível — cada linha aqui é um LancamentoContabil imutável, nunca atualizado in-place.</summary>
public sealed class InMemoryLancamentoContabilRepository : ILancamentoContabilRepository
{
    private readonly ConcurrentDictionary<string, LancamentoContabil> _porId = new();

    public Task<LancamentoContabil?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<LancamentoContabil?> BuscarPorOrigemAsync(string businessId, string origemChave, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(l => l.BusinessId == businessId && l.Origem.Chave == origemChave));

    public Task SalvarAsync(LancamentoContabil lancamento, CancellationToken ct = default)
    {
        // Um LancamentoContabil já persistido nunca é sobrescrito por outro com o mesmo Id — o
        // TryAdd (em vez de indexer) é a garantia física de imutabilidade neste adapter: se algum
        // bug tentasse "salvar de novo" o mesmo lançamento, isso é ignorado, não substituído.
        _porId.TryAdd(lancamento.Id, lancamento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LancamentoContabil>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LancamentoContabil>>(_porId.Values
            .Where(l => l.BusinessId == businessId && l.Data >= inicio && l.Data <= fim)
            .ToList());
}
