using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// Rateio do custo de ENTRADA (landed cost) — herdado do algoritmo provado em produção do
/// gestao-raiz (<c>computeLandedItemCosts</c>, plano §6.1): frete/seguro/outras despesas
/// residuais da nota (o que não foi informado item a item no XML) são rateados
/// PROPORCIONALMENTE ao valor do produto de cada linha — política <c>PorValor</c>, a única
/// implementada nesta entrega (<c>PorPeso</c>/<c>PorQuantidade</c> ficam para quando o rateio de
/// frete de terceiro — plano §6.2 — precisar delas).
///
/// Invariante de ouro (plano §3.3.1): Σ landed_i == vNF SEMPRE, em centavos EXATOS — nunca por
/// aproximação. O rateio proporcional só distribui a diferença de forma justa; quem GARANTE a
/// igualdade é o ÚLTIMO item, que absorve o resíduo de arredondamento (mesma técnica do
/// gestao-raiz) — por isso a ORDEM dos itens entra no cálculo, mesmo sendo irrelevante para o
/// negócio: é só o "quem sobra a última moeda" do arredondamento.
/// </summary>
public static class CustoDeEntrada
{
    public static Result<IReadOnlyList<Money>> Ratear(TotaisDaNota totais, IReadOnlyList<ItemDeNotaDeCompra> itens)
    {
        if (itens.Count == 0)
            return Result.Falhar<IReadOnlyList<Money>>(new Error("compras.custo.sem_itens", "Não há itens para ratear o custo de entrada."));

        var somaVProd = itens.Sum(i => i.VProd.Centavos);
        if (somaVProd <= 0)
            return Result.Falhar<IReadOnlyList<Money>>(new Error(
                "compras.custo.vprod_zero", "Soma dos valores de produto deve ser maior que zero para ratear frete/impostos."));

        var residualFrete = Math.Max(0, totais.VFrete.Centavos - itens.Sum(i => i.VFreteItem?.Centavos ?? 0));
        var residualSeguro = Math.Max(0, totais.VSeguro.Centavos - itens.Sum(i => i.VSegItem?.Centavos ?? 0));
        var residualOutro = Math.Max(0, totais.VOutro.Centavos - itens.Sum(i => i.VOutroItem?.Centavos ?? 0));

        var landed = new long[itens.Count];
        for (var i = 0; i < itens.Count; i++)
        {
            var item = itens[i];
            var acessorioInformado = (item.VFreteItem?.Centavos ?? 0) + (item.VSegItem?.Centavos ?? 0) + (item.VOutroItem?.Centavos ?? 0);
            var residualDoItem =
                RatearPorShare(residualFrete, item.VProd.Centavos, somaVProd) +
                RatearPorShare(residualSeguro, item.VProd.Centavos, somaVProd) +
                RatearPorShare(residualOutro, item.VProd.Centavos, somaVProd);

            landed[i] = item.VProd.Centavos + acessorioInformado + residualDoItem
                + item.VIpi.Centavos + item.VIcmsSt.Centavos - item.VDesc.Centavos;
        }

        // Reconciliação: o ÚLTIMO item absorve o resíduo de arredondamento do rateio proporcional.
        // Depois desta linha Σ landed == vNF é uma igualdade EXATA, não aproximada.
        var somaSemUltimo = landed.Take(itens.Count - 1).Sum();
        landed[^1] = totais.VNf.Centavos - somaSemUltimo;

        return Result.Ok<IReadOnlyList<Money>>(landed.Select(centavos => new Money(centavos)).ToArray());
    }

    private static long RatearPorShare(long total, long numerador, long denominador)
        => denominador == 0 ? 0 : (long)Math.Round((decimal)total * numerador / denominador, MidpointRounding.ToEven);
}
