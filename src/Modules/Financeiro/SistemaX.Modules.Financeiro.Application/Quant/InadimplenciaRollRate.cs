namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>Faixa de atraso de uma parcela em aberto — os baldes clássicos de aging contábil.</summary>
public enum FaixaDeAtraso
{
    EmDia,
    Ate30Dias,
    De31a60Dias,
    De61a90Dias,
    De91a180Dias,
    Acima180Dias,
}

/// <summary>
/// Score de inadimplência por roll-rate sobre os recebíveis (catálogo #3 do plano de inteligência
/// do Financeiro — docs/financeiro/inteligencia-arquitetura.md §4/ADR-0005) — "esse 'a receber'
/// vale quanto de verdade?".
///
/// F1 usa uma curva PADRÃO de perda esperada por faixa de atraso (<see cref="TaxaDePerdaPadrao"/>)
/// — convenção comum de provisionamento por aging usada por PMEs brasileiras na ausência de
/// histórico próprio de transição de carteira. NÃO é fitada aos dados do tenant nesta fase: o
/// sistema hoje não tem snapshots mensais de carteira (isso é <c>fato_recebiveis</c> com histórico
/// — Fase 3 do roadmap) nem um status de "perda confirmada" na FSM de <c>Parcela</c> (só
/// Aberto/Parcial/Pago/Atrasado/Cancelado) para treinar uma matriz de roll-rate empírica de
/// verdade. <see cref="EstimarMatrizRollRate"/> já existe pronta para quando essa base chegar —
/// não é chamada pelo pipeline de produção ainda.
/// </summary>
public static class InadimplenciaRollRate
{
    public static readonly IReadOnlyDictionary<FaixaDeAtraso, double> TaxaDePerdaPadrao = new Dictionary<FaixaDeAtraso, double>
    {
        [FaixaDeAtraso.EmDia] = 0.00,
        [FaixaDeAtraso.Ate30Dias] = 0.02,
        [FaixaDeAtraso.De31a60Dias] = 0.10,
        [FaixaDeAtraso.De61a90Dias] = 0.25,
        [FaixaDeAtraso.De91a180Dias] = 0.50,
        [FaixaDeAtraso.Acima180Dias] = 0.90,
    };

    /// <summary>Classifica dias de atraso em faixa. <paramref name="diasAtraso"/> ≤ 0 (a vencer ou
    /// vence hoje) é sempre <see cref="FaixaDeAtraso.EmDia"/>.</summary>
    public static FaixaDeAtraso ClassificarFaixa(int diasAtraso) => diasAtraso switch
    {
        <= 0 => FaixaDeAtraso.EmDia,
        <= 30 => FaixaDeAtraso.Ate30Dias,
        <= 60 => FaixaDeAtraso.De31a60Dias,
        <= 90 => FaixaDeAtraso.De61a90Dias,
        <= 180 => FaixaDeAtraso.De91a180Dias,
        _ => FaixaDeAtraso.Acima180Dias,
    };

    public sealed record ParcelaEmAberto(string ParcelaId, long ValorEmAbertoCentavos, int DiasAtraso);

    public sealed record ResumoFaixa(long ValorCentavos, long ProvisaoCentavos, int Quantidade);

    public sealed record ResultadoProvisao(
        long ValorTotalEmAbertoCentavos,
        long ProvisaoEsperadaCentavos,
        IReadOnlyDictionary<FaixaDeAtraso, ResumoFaixa> PorFaixa);

    /// <summary>
    /// Provisão para devedores duvidosos = Σ (valor em aberto da faixa × taxa de perda da faixa) —
    /// a mesma lógica de PDD/aging usada em contabilidade, aplicada por FAIXA DE ATRASO (não por
    /// rating de crédito, que este sistema não modela).
    /// </summary>
    public static ResultadoProvisao CalcularProvisao(
        IReadOnlyList<ParcelaEmAberto> parcelas,
        IReadOnlyDictionary<FaixaDeAtraso, double>? taxasDePerda = null)
    {
        var taxas = taxasDePerda ?? TaxaDePerdaPadrao;
        var acumulador = new Dictionary<FaixaDeAtraso, (long Valor, long Provisao, int Qtd)>();

        foreach (var parcela in parcelas)
        {
            var faixa = ClassificarFaixa(parcela.DiasAtraso);
            var taxa = taxas.GetValueOrDefault(faixa, 0.0);
            var provisao = (long)Math.Round(parcela.ValorEmAbertoCentavos * taxa, MidpointRounding.ToEven);

            var atual = acumulador.GetValueOrDefault(faixa, (0L, 0L, 0));
            acumulador[faixa] = (atual.Item1 + parcela.ValorEmAbertoCentavos, atual.Item2 + provisao, atual.Item3 + 1);
        }

        var porFaixa = acumulador.ToDictionary(kv => kv.Key, kv => new ResumoFaixa(kv.Value.Item1, kv.Value.Item2, kv.Value.Item3));

        return new ResultadoProvisao(
            parcelas.Sum(p => p.ValorEmAbertoCentavos),
            porFaixa.Values.Sum(r => r.ProvisaoCentavos),
            porFaixa);
    }

    /// <summary>
    /// Estimador GENÉRICO de matriz de roll-rate a partir de transições observadas (faixa em T →
    /// faixa em T+1 da MESMA parcela, dois snapshots consecutivos da carteira) — matriz
    /// row-estocástica (cada linha soma 1) com suavização de Laplace (+1 em toda célula antes de
    /// normalizar), que nunca zera uma linha cuja faixa de origem não teve nenhuma transição
    /// observada no período informado.
    ///
    /// PREPARADO PARA A FASE 3 (quando <c>fato_recebiveis</c> tiver snapshots mensais da carteira)
    /// — não é chamado pelo pipeline de produção da F1; <see cref="CalcularProvisao"/> usa
    /// <see cref="TaxaDePerdaPadrao"/> como ponto de partida até essa base existir.
    /// </summary>
    public static IReadOnlyDictionary<FaixaDeAtraso, IReadOnlyDictionary<FaixaDeAtraso, double>> EstimarMatrizRollRate(
        IReadOnlyList<(FaixaDeAtraso De, FaixaDeAtraso Para)> transicoesObservadas)
    {
        var todasAsFaixas = Enum.GetValues<FaixaDeAtraso>();
        var resultado = new Dictionary<FaixaDeAtraso, IReadOnlyDictionary<FaixaDeAtraso, double>>();

        foreach (var origem in todasAsFaixas)
        {
            var contagens = todasAsFaixas.ToDictionary(destino => destino, _ => 1.0); // Laplace: +1 em toda célula
            foreach (var (de, para) in transicoesObservadas)
            {
                if (de == origem) contagens[para] += 1.0;
            }

            var total = contagens.Values.Sum();
            resultado[origem] = contagens.ToDictionary(kv => kv.Key, kv => kv.Value / total);
        }

        return resultado;
    }
}
