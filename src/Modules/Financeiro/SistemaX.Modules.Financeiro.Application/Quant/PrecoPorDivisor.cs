namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// LAR ÚNICO do "preço por divisor" (docs/financeiro/ideias-matemonstro.md, idéia 2 — "Preço-piso
/// e margem real por forma de pagamento") — função PURA, nenhum port, nenhum dado próprio. Serve o
/// cluster de %-sobre-preço dos verticais MEI-alvo: marketplace/e-commerce de vestuário (12–20%),
/// delivery (iFood 20–27%) e comissão de profissional em beleza/serviço.
///
/// O erro que "mais quebra comércio" (trilha <c>fin-gestao-01</c> do matemonstro): aplicar markup
/// MULTIPLICANDO o custo por <c>(1 + Σ%)</c> quando imposto/taxa de cartão/comissão incidem sobre o
/// PREÇO FINAL, não sobre o custo — isso SUBESTIMA o preço mínimo necessário, porque um preço maior
/// gera mais imposto e mais taxa (efeito recursivo que o multiplicador ignora). A única forma
/// correta é o DIVISOR:
///
///     PrecoMinimo = Custo ÷ (1 − Σ percentuaisSobrePreco − margemDesejada)
///
/// e o PREÇO-PISO é o mesmo com <c>margemDesejada = 0</c>: o menor preço que ainda cobre custo e
/// todos os custos percentuais — o limite absoluto de qualquer desconto. Invariante que separa os
/// dois métodos (travada em teste): <c>PrecoPorDivisor &gt; Custo × (1 + Σ%)</c> para os mesmos
/// percentuais — o divisor SEMPRE pede um preço maior que o markup multiplicador ingênuo.
/// </summary>
public static class PrecoPorDivisor
{
    /// <param name="PrecoSugeridoCentavos">Divisor com a margem desejada embutida — o preço "de
    /// tabela" que entrega a margem pedida depois de descontar todos os percentuais sobre o
    /// preço.</param>
    /// <param name="PrecoPisoCentavos">Divisor com <c>margemDesejada = 0</c> — o menor preço que
    /// ainda cobre custo + percentuais, sem sobrar margem nenhuma. Teto de qualquer desconto.</param>
    /// <param name="SomaPercentuaisSobrePreco">Eco do somatório usado (MDR + alíquota efetiva +
    /// comissão, já como fração) — conveniência para quem exibe o resultado.</param>
    /// <param name="MargemRealNoPrecoAtualPercent"><c>null</c> quando <c>precoAtualCentavos</c> não
    /// foi informado; senão, a margem que o preço ATUAL de fato entrega depois dos percentuais —
    /// "no crédito, este item rende só X% real" (o fato de Consultor que a idéia 2 do matemonstro
    /// propõe).</param>
    public sealed record Resultado(
        long PrecoSugeridoCentavos, long PrecoPisoCentavos, decimal SomaPercentuaisSobrePreco, decimal? MargemRealNoPrecoAtualPercent);

    /// <summary>
    /// <paramref name="percentuaisSobrePreco"/> = MDR + alíquota efetiva do Simples + comissão — a
    /// soma de TUDO que incide sobre o preço final, nunca sobre o custo. Cada chamador resolve o
    /// valor já como fração (0.0349 = 3,49%) a partir do LAR de cada taxa
    /// (<c>FormaDePagamento.TaxaPercentual</c>/<c>RadarDoSimplesResultado.AliquotaEfetiva</c>) —
    /// esta função nunca recalcula taxa nenhuma, só soma e divide.
    ///
    /// Sem raiz válida (soma dos percentuais + margem ≥ 100% — pedido matematicamente impossível:
    /// nenhum preço finito cobre isso) devolve <c>null</c>, nunca um preço negativo/infinito
    /// disfarçado de número (mesmo racional de <see cref="TaxaInternaDeRetorno.Resultado.Indefinida"/>).
    /// Arredondamento SEMPRE para cima (<c>Math.Ceiling</c>) — um preço-piso/sugerido nunca pode
    /// cair abaixo do necessário por arredondamento bancário.
    /// </summary>
    public static Resultado? Calcular(
        long custoCentavos, IReadOnlyList<decimal> percentuaisSobrePreco, decimal margemDesejada, long? precoAtualCentavos = null)
    {
        if (custoCentavos < 0)
            throw new ArgumentOutOfRangeException(nameof(custoCentavos), "Custo não pode ser negativo.");
        if (margemDesejada < 0)
            throw new ArgumentOutOfRangeException(nameof(margemDesejada), "Margem desejada não pode ser negativa.");

        var somaPercentuais = percentuaisSobrePreco.Sum();
        if (somaPercentuais < 0)
            throw new ArgumentOutOfRangeException(nameof(percentuaisSobrePreco), "Percentuais sobre preço não podem ser negativos.");

        var divisorPiso = 1m - somaPercentuais;
        var divisorSugerido = 1m - somaPercentuais - margemDesejada;

        if (divisorPiso <= 0 || divisorSugerido <= 0) return null;

        var precoPiso = (long)Math.Ceiling(custoCentavos / divisorPiso);
        var precoSugerido = (long)Math.Ceiling(custoCentavos / divisorSugerido);

        decimal? margemRealNoPrecoAtual = precoAtualCentavos is > 0
            ? Math.Round((1m - somaPercentuais) - custoCentavos / (decimal)precoAtualCentavos.Value, 4)
            : null;

        return new Resultado(precoSugerido, precoPiso, somaPercentuais, margemRealNoPrecoAtual);
    }
}
