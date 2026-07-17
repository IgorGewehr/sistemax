using System.Collections.Concurrent;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Infrastructure.InMemory;

public sealed class InMemoryFatoMargemProdutoRepository : IFatoMargemProdutoRepository
{
    private sealed record Acumulado(long ReceitaCentavos, long CustoCentavos);

    private readonly ConcurrentDictionary<(string TenantId, string ProdutoId, DateOnly Dia), Acumulado> _fatos = new();
    private readonly ConcurrentDictionary<(string TenantId, string VendaId), (DateOnly Dia, List<ItemMargemPendente> Itens)> _pendentes = new();
    private readonly object _lock = new();

    public Task RegistrarItensDeVendaAsync(string tenantId, string vendaId, DateOnly dia, IReadOnlyList<ItemMargemPendente> itens, CancellationToken ct = default)
    {
        lock (_lock)
        {
            foreach (var item in itens)
            {
                var chave = (tenantId, item.ProdutoId, dia);
                var atual = _fatos.GetValueOrDefault(chave, new Acumulado(0, 0));
                _fatos[chave] = atual with { ReceitaCentavos = atual.ReceitaCentavos + item.ReceitaItemCentavos };
            }

            _pendentes[(tenantId, vendaId)] = (dia, itens.ToList());
        }

        return Task.CompletedTask;
    }

    public Task AlocarCustoDeVendaAsync(string tenantId, string vendaId, long custoTotalCentavos, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_pendentes.TryRemove((tenantId, vendaId), out var pendente)) return Task.CompletedTask;

            var pesos = pendente.Itens.Select(i => i.ReceitaItemCentavos).ToList();
            var alocado = RateioProporcional.Alocar(custoTotalCentavos, pesos);

            for (var i = 0; i < pendente.Itens.Count; i++)
            {
                var chave = (tenantId, pendente.Itens[i].ProdutoId, pendente.Dia);
                var atual = _fatos.GetValueOrDefault(chave, new Acumulado(0, 0));
                _fatos[chave] = atual with { CustoCentavos = atual.CustoCentavos + alocado[i] };
            }
        }

        return Task.CompletedTask;
    }

    public Task<FatoMargemProduto?> ObterAsync(string tenantId, string produtoId, DateOnly dia, CancellationToken ct = default)
        => Task.FromResult(_fatos.TryGetValue((tenantId, produtoId, dia), out var a)
            ? new FatoMargemProduto(tenantId, produtoId, dia, a.ReceitaCentavos, a.CustoCentavos, DateTimeOffset.UtcNow)
            : null);

    public Task<IReadOnlyList<FatoMargemProduto>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoMargemProduto>>(_fatos
            .Where(kv => kv.Key.TenantId == tenantId && kv.Key.Dia >= de && kv.Key.Dia <= ate)
            .OrderBy(kv => kv.Key.Dia).ThenBy(kv => kv.Key.ProdutoId)
            .Select(kv => new FatoMargemProduto(tenantId, kv.Key.ProdutoId, kv.Key.Dia, kv.Value.ReceitaCentavos, kv.Value.CustoCentavos, DateTimeOffset.UtcNow))
            .ToList());

    public Task<IReadOnlyList<FatoMargemProduto>> ListarPorProdutoAsync(string tenantId, string produtoId, DateOnly de, DateOnly ate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FatoMargemProduto>>(_fatos
            .Where(kv => kv.Key.TenantId == tenantId && kv.Key.ProdutoId == produtoId && kv.Key.Dia >= de && kv.Key.Dia <= ate)
            .OrderBy(kv => kv.Key.Dia)
            .Select(kv => new FatoMargemProduto(tenantId, produtoId, kv.Key.Dia, kv.Value.ReceitaCentavos, kv.Value.CustoCentavos, DateTimeOffset.UtcNow))
            .ToList());

    public Task ZerarTudoAsync(CancellationToken ct = default)
    {
        _fatos.Clear();
        _pendentes.Clear();
        return Task.CompletedTask;
    }
}
