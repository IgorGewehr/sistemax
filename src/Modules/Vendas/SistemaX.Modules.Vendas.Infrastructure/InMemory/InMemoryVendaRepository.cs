using System.Collections.Concurrent;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Domain;

namespace SistemaX.Modules.Vendas.Infrastructure.InMemory;

/// <summary>
/// Adapter direto in-memory — suficiente para rodar o módulo e os testes sem infraestrutura
/// externa. EXTENSÍVEL PARA SQLITE: trocar o dicionário por persistência real do agregado a cada
/// mudança (não só na conclusão — ver nota de crash-safety em <see cref="IVendaRepository"/>)
/// mantendo exatamente esta interface de port; nenhum código de Application/Domain muda.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/> — suficiente para um terminal
/// de PDV (processo único local), não para concorrência distribuída multi-nó.
/// </summary>
public sealed class InMemoryVendaRepository : IVendaRepository
{
    private readonly ConcurrentDictionary<string, Venda> _porId = new();

    public Task<Venda?> ObterPorIdAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_porId.GetValueOrDefault(id));

    public Task SalvarAsync(Venda venda, CancellationToken ct = default)
    {
        _porId[venda.Id] = venda;
        return Task.CompletedTask;
    }
}
