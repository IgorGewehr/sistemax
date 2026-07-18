using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Nomenclatura clássica de engenharia de cardápio (Kasavana &amp; Smith): Estrela = margem
/// alta + popular; VacaLeiteira ("plowhorse") = popular, margem baixa; Enigma ("puzzle") = margem
/// alta, pouco popular; Abacaxi ("dog") = os dois baixos.</summary>
public enum QuadranteCardapio
{
    Estrela,
    VacaLeiteira,
    Enigma,
    Abacaxi
}

public sealed record LinhaEngenhariaCardapio(
    string ProdutoId, long ReceitaCentavos, double MargemContribuicaoPercent, decimal ParticipacaoNaReceitaPercent, QuadranteCardapio Quadrante);

/// <summary>
/// LENTE VERTICAL ALIMENTAÇÃO/DELIVERY (opt-in) — matriz margem × popularidade → 4 quadrantes.
/// ZERO DADO NOVO: mesma fonte do <see cref="FoodCostService"/> (<c>fato_margem_produto</c>) — é o
/// quadrante volume×MC do catálogo #6 do plano de inteligência (docs/financeiro/ideias-matemonstro.md
/// "Restaurantes"), só RE-RÓTULADO com o nome comercial do vertical, nenhuma matemática nova.
///
/// "POPULARIDADE" é aproximada pela PARTICIPAÇÃO NA RECEITA do produto dentro do período — NUNCA
/// unidades vendidas: o razão de estoque (onde a quantidade vendida vive) não pode ser consultado
/// direto por este módulo (ARCHITECTURE.md §1.2 — Financeiro só ouve os eventos que o Estoque já
/// publica, nunca chama de volta), e adicionar uma coluna de quantidade a <c>fato_margem_produto</c>
/// tocaria o núcleo/motor quant compartilhado (breakeven, radar) só para alimentar uma lente
/// opt-in — exatamente o "complicar o núcleo" que a tarefa pede pra evitar. Participação na
/// receita é uma aproximação DOCUMENTADA e honesta, não uma segunda fonte de verdade de volume.
///
/// SPLIT POINT de cada eixo = MÉDIA do próprio grupo no período (mesmo espírito determinístico do
/// resto do motor quant, ex. corte 80/95 da Curva ABC): margem acima da margem média do grupo =
/// "margem alta"; participação acima de <c>100%/N</c> (a fatia que cada produto teria se todos
/// vendessem igual) = "popular". Threshold recalculado a cada chamada — nunca hardcoded.
///
/// OPT-IN por presença de dado: sem nenhuma linha de receita no período, devolve lista vazia
/// (fail-quiet — a lente não aparece pra quem não vende prato).
/// </summary>
public sealed class EngenhariaDeCardapioService(IFatoMargemProdutoRepository fatoMargemProduto)
{
    public async Task<IReadOnlyList<LinhaEngenhariaCardapio>> ClassificarAsync(
        string businessId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var fatos = await fatoMargemProduto.ListarAsync(businessId, de, ate, ct).ConfigureAwait(false);

        var porProduto = fatos
            .GroupBy(f => f.ProdutoId)
            .Select(g => (ProdutoId: g.Key, Receita: g.Sum(f => f.ReceitaCentavos), Custo: g.Sum(f => f.CustoCentavos)))
            .Where(item => item.Receita > 0)
            .ToList();

        if (porProduto.Count == 0) return [];

        var receitaTotal = porProduto.Sum(item => item.Receita);
        var margemMedia = porProduto.Average(item => (double)(item.Receita - item.Custo) / item.Receita);
        var participacaoMedia = 100m / porProduto.Count;

        var resultado = new List<LinhaEngenhariaCardapio>();
        foreach (var item in porProduto)
        {
            var margemPercent = (double)(item.Receita - item.Custo) / item.Receita;
            var participacao = Math.Round(100m * item.Receita / receitaTotal, 2);

            var margemAlta = margemPercent >= margemMedia;
            var popular = participacao >= participacaoMedia;

            var quadrante = (margemAlta, popular) switch
            {
                (true, true) => QuadranteCardapio.Estrela,
                (false, true) => QuadranteCardapio.VacaLeiteira,
                (true, false) => QuadranteCardapio.Enigma,
                (false, false) => QuadranteCardapio.Abacaxi,
            };

            resultado.Add(new LinhaEngenhariaCardapio(item.ProdutoId, item.Receita, margemPercent, participacao, quadrante));
        }

        return resultado.OrderByDescending(linha => linha.ReceitaCentavos).ToList();
    }
}
