using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryConciliacaoRepository : IConciliacaoRepository
{
    private readonly ConcurrentDictionary<string, Conciliacao> _porId = new();

    public Task<Conciliacao?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task<Conciliacao?> BuscarPorParAsync(string movimentoFinanceiroId, string extratoBancarioItemId, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(c =>
            c.MovimentoFinanceiroId == movimentoFinanceiroId && c.ExtratoBancarioItemId == extratoBancarioItemId));

    public Task SalvarAsync(Conciliacao conciliacao, CancellationToken ct = default)
    {
        _porId[conciliacao.Id] = conciliacao;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Conciliacao>> ListarPorBusinessIdAsync(string businessId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Conciliacao>>(_porId.Values.Where(c => c.BusinessId == businessId).ToList());
}

public sealed class InMemoryExtratoBancarioItemRepository : IExtratoBancarioItemRepository
{
    private readonly ConcurrentDictionary<string, ExtratoBancarioItem> _porId = new();

    public Task<ExtratoBancarioItem?> BuscarPorIdentificadorExternoAsync(string businessId, string identificadorExterno, CancellationToken ct = default)
        => Task.FromResult(_porId.Values.FirstOrDefault(i => i.BusinessId == businessId && i.IdentificadorExterno == identificadorExterno));

    public Task SalvarAsync(ExtratoBancarioItem item, CancellationToken ct = default)
    {
        _porId[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExtratoBancarioItem>> ListarNaoConciliadosAsync(string businessId, string contaBancariaCaixaId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ExtratoBancarioItem>>(_porId.Values
            .Where(i => i.BusinessId == businessId && i.ContaBancariaCaixaId == contaBancariaCaixaId)
            .ToList());
}
