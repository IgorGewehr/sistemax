using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record LinhaFoodCost(string ProdutoId, long ReceitaCentavos, long CustoCentavos, double FoodCostPercent);

/// <summary>
/// LENTE VERTICAL ALIMENTAÇÃO/DELIVERY (opt-in) — Food cost % = CMV do prato ÷ receita do prato,
/// agregado no período. ZERO DADO NOVO: reusa <c>fato_margem_produto</c>
/// (<see cref="Analitico.FatoMargemProduto"/>, já existente para o quadrante margem×popularidade
/// do catálogo #6) — receita e CMV por produto/dia, foldados do ledger.
///
/// A FICHA TÉCNICA (BOM/<c>ComponenteDeFicha</c>) já está DENTRO do <c>CustoCentavos</c> sem este
/// read-model precisar saber nada de BOM: quando o Estoque baixa a venda de um prato composto, o
/// <c>ExpansorDeFichaTecnica</c> expande recursivamente nos insumos-folha ANTES de gerar os
/// movimentos de saída, e <c>CustoBaixadoPorVenda</c> nasce da soma desses insumos — o CMV que
/// chega aqui já É o custo da ficha técnica. Reuso de fórmula por construção, não por chamada
/// cross-módulo (que a arquitetura proíbe — ver ARCHITECTURE.md §1.2: Financeiro nunca chama
/// Estoque direto, só ouve os eventos que ele já publica).
///
/// OPT-IN por presença de dado (regra "sem complicar" da tarefa): produto sem venda no período
/// simplesmente não aparece na lista — fail-quiet, nunca um card de "0% de tudo". A UI decide
/// mostrar esta lente só pra quem vende prato/ficha técnica; este read-model não precisa gate
/// próprio (endpoint sempre existe, a lista fica vazia pra quem não tem o dado).
/// </summary>
public sealed class FoodCostService(IFatoMargemProdutoRepository fatoMargemProduto)
{
    public async Task<IReadOnlyList<LinhaFoodCost>> CalcularAsync(
        string businessId, DateOnly de, DateOnly ate, string? produtoId = null, CancellationToken ct = default)
    {
        var fatos = produtoId is null
            ? await fatoMargemProduto.ListarAsync(businessId, de, ate, ct).ConfigureAwait(false)
            : await fatoMargemProduto.ListarPorProdutoAsync(businessId, produtoId, de, ate, ct).ConfigureAwait(false);

        return fatos
            .GroupBy(f => f.ProdutoId)
            .Select(g => (ProdutoId: g.Key, Receita: g.Sum(f => f.ReceitaCentavos), Custo: g.Sum(f => f.CustoCentavos)))
            .Where(item => item.Receita > 0) // sem receita não há food cost % a reportar (divisão indefinida)
            .Select(item => new LinhaFoodCost(item.ProdutoId, item.Receita, item.Custo, (double)item.Custo / item.Receita))
            .OrderByDescending(linha => linha.FoodCostPercent)
            .ToList();
    }
}
