namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Rateio proporcional de um total inteiro (centavos) por uma lista de pesos — o "método do maior
/// resto" (Hamilton/Largest Remainder), o mesmo algoritmo usado para distribuir cadeiras de
/// parlamento sem violar a soma total. Usado por <c>FatoMargemProdutoProjection</c> para alocar
/// <c>CustoBaixadoPorVenda</c> (total por venda) entre os produtos da venda, proporcional à
/// receita de cada item — <b>garante por construção</b> que a soma dos valores alocados é
/// EXATAMENTE <paramref name="total"/>, nunca um centavo a mais ou a menos (arredondamento puro
/// por truncamento deixaria sobra; distribuir a sobra por "quem tem mais peso primeiro" é a
/// correção padrão, auditável linha a linha).
/// </summary>
public static class RateioProporcional
{
    /// <summary>
    /// Aloca <paramref name="total"/> entre os pesos, proporcional a cada peso, com a soma dos
    /// resultados sempre igual a <paramref name="total"/>. Pesos negativos são tratados como 0 na
    /// ponderação (não fazem sentido de negócio aqui — receita de item não é negativa); se a soma
    /// de pesos for 0 (ex.: todos os itens 100% descontados), distribui igualmente. Comportamento
    /// com <paramref name="total"/> negativo não é usado nem testado — CMV é sempre ≥ 0 na origem
    /// (<c>VendaItensMovimentadosHandler</c> só publica <c>CustoBaixadoPorVenda</c> quando o total
    /// é positivo).
    /// </summary>
    public static IReadOnlyList<long> Alocar(long total, IReadOnlyList<long> pesos)
    {
        if (pesos.Count == 0) return [];
        if (total == 0) return pesos.Select(_ => 0L).ToList();

        var pesosNaoNegativos = pesos.Select(p => Math.Max(0, p)).ToList();
        var somaPesos = pesosNaoNegativos.Sum();
        if (somaPesos <= 0) return DistribuirIgualmente(total, pesos.Count);

        var brutos = new long[pesos.Count];
        var restos = new double[pesos.Count];
        long somaAlocada = 0;

        for (var i = 0; i < pesos.Count; i++)
        {
            var exato = (double)total * pesosNaoNegativos[i] / somaPesos;
            brutos[i] = (long)Math.Floor(exato);
            restos[i] = exato - brutos[i];
            somaAlocada += brutos[i];
        }

        // Sobra de arredondamento — sempre < pesos.Count, pois cada erro de floor é < 1.
        var faltam = (int)(total - somaAlocada);
        var ordemPorMaiorResto = Enumerable.Range(0, pesos.Count).OrderByDescending(i => restos[i]).ThenBy(i => i);
        foreach (var indice in ordemPorMaiorResto.Take(faltam))
        {
            brutos[indice] += 1;
        }

        return brutos;
    }

    private static IReadOnlyList<long> DistribuirIgualmente(long total, int quantidade)
    {
        var baseValor = total / quantidade;
        var resto = total - baseValor * quantidade;
        var resultado = new long[quantidade];
        for (var i = 0; i < quantidade; i++)
        {
            resultado[i] = baseValor + (i < resto ? 1 : 0);
        }

        return resultado;
    }
}
