using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Application.Comum;

/// <summary>
/// Expande recursivamente a ficha técnica (BOM) de um produto composto até os insumos-folha —
/// a baixa de estoque NUNCA atinge um produto com <c>FichaTecnica</c> não-vazia diretamente.
/// Profundidade máxima 3 (mesmo limite do saas-erp): mantém o replay barato e serve de guarda
/// defensiva contra ciclo não detectado na composição.
/// </summary>
public static class ExpansorDeFichaTecnica
{
    private const int ProfundidadeMaxima = 3;

    public static async Task<Result<IReadOnlyList<(string ProdutoId, Quantidade Quantidade)>>> ExpandirAsync(
        IProdutoRepository produtos, string produtoId, Quantidade quantidade, CancellationToken ct = default)
    {
        var acumulado = new List<(string ProdutoId, Quantidade Quantidade)>();
        var visitados = new HashSet<string>();

        var resultado = await ExpandirRecursivoAsync(produtos, produtoId, quantidade, 0, visitados, acumulado, ct);

        return resultado.Falha
            ? Result.Falhar<IReadOnlyList<(string, Quantidade)>>(resultado.Erro)
            : Result.Ok<IReadOnlyList<(string, Quantidade)>>(acumulado);
    }

    private static async Task<Result> ExpandirRecursivoAsync(
        IProdutoRepository produtos, string produtoId, Quantidade quantidade, int profundidade,
        HashSet<string> visitados, List<(string, Quantidade)> acumulado, CancellationToken ct)
    {
        if (profundidade > ProfundidadeMaxima)
            return Result.Falhar(new Error(
                "estoque.bom.profundidade_excedida",
                $"Ficha técnica excede profundidade máxima de {ProfundidadeMaxima} níveis (possível ciclo envolvendo '{produtoId}')."));

        if (!visitados.Add(produtoId))
            return Result.Falhar(new Error("estoque.bom.ciclo_detectado", $"Ciclo detectado na ficha técnica envolvendo o produto '{produtoId}'."));

        var produto = await produtos.ObterPorIdAsync(produtoId, ct);
        if (produto is null || produto.FichaTecnica.Count == 0)
        {
            acumulado.Add((produtoId, quantidade));
            visitados.Remove(produtoId);
            return Result.Ok();
        }

        foreach (var componente in produto.FichaTecnica)
        {
            var quantidadeComponente = Quantidade.DeDecimal(componente.Quantidade.EmDecimal * quantidade.EmDecimal);
            var subResultado = await ExpandirRecursivoAsync(
                produtos, componente.ProdutoInsumoId, quantidadeComponente, profundidade + 1, visitados, acumulado, ct);
            if (subResultado.Falha) return subResultado;
        }

        visitados.Remove(produtoId);
        return Result.Ok();
    }
}
