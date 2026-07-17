using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.ReadModels;

public sealed record LinhaCurvaAbc(
    string ProdutoId, string ProdutoNome, char Classe, Quantidade QuantidadeSaida, Money ValorDeCusto, decimal PercentualAcumulado);

/// <summary>
/// Curva ABC — corte 80/15/5 clássico, por VALOR DE CUSTO baixado no período (CMV congelado em
/// cada <c>Saida</c> do razão). NOTA DE ESCOPO: o razão de estoque não retém preço de VENDA (isso
/// vive no evento de origem da venda, fora deste módulo) — uma curva por RECEITA real pertence a
/// um read-model de Vendas que cruze os dois. Aqui a curva reflete capital que efetivamente saiu
/// do estoque, o que já é o suficiente para responder "o que concentra o giro" e "o que é
/// capital parado" (classe C).
/// </summary>
public sealed class CurvaAbcService(IMovimentoRepository movimentos, IProdutoRepository produtos)
{
    private const decimal CorteClasseA = 80m;
    private const decimal CorteClasseB = 95m;

    public async Task<IReadOnlyList<LinhaCurvaAbc>> ClassificarAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var saidasAgrupadas = (await movimentos.ListarPorPeriodoAsync(tenantId, inicio, fim, ct))
            .Where(m => m.Tipo == TipoMovimento.Saida)
            .GroupBy(m => m.ProdutoId)
            .Select(grupo => new
            {
                ProdutoId = grupo.Key,
                Quantidade = grupo.Aggregate(Quantidade.Zero, (acumulado, m) => acumulado + m.Quantidade),
                Valor = grupo.Aggregate(Money.Zero, (acumulado, m) => acumulado + CustoDoMovimento(m))
            })
            .Where(item => item.Valor.EhPositivo)
            .OrderByDescending(item => item.Valor.Centavos)
            .ToList();

        var totalValor = saidasAgrupadas.Sum(item => item.Valor.Centavos);
        if (totalValor == 0) return [];

        var produtosPorId = (await produtos.ListarAsync(tenantId, ct)).ToDictionary(p => p.Id);
        var resultado = new List<LinhaCurvaAbc>();
        long acumulado = 0;

        foreach (var item in saidasAgrupadas)
        {
            acumulado += item.Valor.Centavos;
            var percentualAcumulado = Math.Round((decimal)acumulado / totalValor * 100m, 1);
            var classe = percentualAcumulado <= CorteClasseA ? 'A' : percentualAcumulado <= CorteClasseB ? 'B' : 'C';
            var nome = produtosPorId.TryGetValue(item.ProdutoId, out var produto) ? produto.Nome : item.ProdutoId;

            resultado.Add(new LinhaCurvaAbc(item.ProdutoId, nome, classe, item.Quantidade, item.Valor, percentualAcumulado));
        }

        return resultado;
    }

    private static Money CustoDoMovimento(MovimentoDeEstoque movimento)
        => new((long)Math.Round((decimal)movimento.Quantidade.Milesimos * movimento.CustoUnitario.Centavos / 1000m, MidpointRounding.ToEven));
}
