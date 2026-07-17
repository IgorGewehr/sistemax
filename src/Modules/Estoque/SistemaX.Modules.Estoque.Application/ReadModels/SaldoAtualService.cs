using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.ReadModels;

/// <summary>Linha de posição atual — o que alimenta a lista de Produtos e o KPI row da Visão
/// Geral do mockup.</summary>
public sealed record PosicaoDeItem(
    string ProdutoId, string ProdutoNome, string? Categoria, string Sku,
    Quantidade Fisico, Quantidade Reservado, Quantidade Disponivel,
    Money CustoMedio, Money ValorTotal, bool AbaixoDoMinimo, bool Zerado);

/// <summary>Posição AGORA (não retroativa — para isso, replay do razão até uma data de corte é
/// responsabilidade de um serviço à parte, fora do escopo desta entrega — ver README §Roadmap).</summary>
public sealed class SaldoAtualService(IProdutoRepository produtos, ISaldoRepository saldos)
{
    public async Task<IReadOnlyList<PosicaoDeItem>> ObterPosicaoAsync(string tenantId, CancellationToken ct = default)
    {
        var todosSaldos = await saldos.ListarAsync(tenantId, ct);
        var produtosPorId = (await produtos.ListarAsync(tenantId, ct)).ToDictionary(p => p.Id);

        var posicoes = new List<PosicaoDeItem>();
        foreach (var saldo in todosSaldos)
        {
            if (!produtosPorId.TryGetValue(saldo.ProdutoId, out var produto)) continue;

            posicoes.Add(new PosicaoDeItem(
                produto.Id, produto.Nome, produto.Categoria, produto.Sku,
                saldo.Fisico, saldo.Reservado, saldo.Disponivel, saldo.CustoMedio, saldo.ValorTotal,
                AbaixoDoMinimo: !produto.EstoqueMinimo.EhZero && saldo.Disponivel <= produto.EstoqueMinimo,
                Zerado: saldo.Disponivel.EhZero || saldo.Disponivel.EhNegativa));
        }

        return posicoes;
    }

    public async Task<Money> ValorTotalEmEstoqueAsync(string tenantId, CancellationToken ct = default)
        => (await ObterPosicaoAsync(tenantId, ct)).Aggregate(Money.Zero, static (acumulado, item) => acumulado + item.ValorTotal);
}
