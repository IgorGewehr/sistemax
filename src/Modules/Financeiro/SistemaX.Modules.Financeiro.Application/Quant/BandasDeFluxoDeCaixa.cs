namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Previsão de caixa com BANDAS P5/P50/P95 (catálogo #1 do plano de inteligência do Financeiro,
/// docs/financeiro/inteligencia-arquitetura.md §4/ADR-0005) — responde "quando fico sem caixa? com
/// que certeza?" com um INTERVALO, não um número único.
///
/// MÉTODO — block bootstrap com seed fixa (ver <see cref="SeedDeterministico"/>):
/// 1. O componente CONHECIDO do caixa futuro (parcelas a receber/pagar já agendadas, mesmo dado que
///    <c>FluxoDeCaixaService</c> já projeta) entra como <see cref="PontoConhecido"/> — determinístico,
///    sem simulação.
/// 2. O componente de INCERTEZA (venda avulsa em dinheiro/pix, o "ruído" do dia a dia que não está
///    em nenhuma parcela) é modelado por RESSAMPLING do histórico de caixa REALIZADO
///    (<c>fato_caixa_diario</c>): sorteiam-se blocos CONTÍGUOS de <paramref name="tamanhoDoBloco"/>
///    dias do histórico (block bootstrap, não i.i.d. — preserva autocorrelação de dia-da-semana,
///    ex. sábado sempre mais forte que segunda) e concatenam-se até cobrir o horizonte de projeção.
/// 3. Repete-se <paramref name="numeroDeSimulacoes"/> vezes; para cada dia futuro, os percentis
///    empíricos 5/50/95 da distribuição de saldos acumulados simulados são as bandas.
///
/// APROXIMAÇÃO DOCUMENTADA: o ruído histórico e o componente conhecido são somados como se fossem
/// independentes — na prática uma venda que gerou o "ruído" histórico de um dia pode também ter
/// virado uma parcela que hoje está no componente conhecido; a F1 aceita essa simplificação (é uma
/// prova de "intervalo com incerteza modelada", não uma reconciliação contábil perfeita entre as
/// duas fontes). Refinamentos ficam para fases seguintes.
///
/// REPRODUTIBILIDADE: com o MESMO <paramref name="seed"/> e os MESMOS insumos, o resultado é
/// byte-a-byte idêntico — <see cref="System.Random"/> com seed explícita é determinístico dentro
/// de uma mesma versão major do runtime .NET (é a mesma garantia com que o resto do ecossistema
/// .NET já conta para testes com seed fixa).
/// </summary>
public static class BandasDeFluxoDeCaixa
{
    /// <summary>Um dia do fluxo CONHECIDO (parcelas já agendadas) — <see cref="DiaOffset"/> é 1
    /// para "amanhã", 2 para "depois de amanhã" etc. (nunca 0 — hoje é o ponto de partida
    /// <paramref name="saldoAtualCentavos"/>, não faz parte da projeção).</summary>
    public sealed record PontoConhecido(int DiaOffset, long DeltaCentavos);

    public sealed record BandaDia(int DiaOffset, long P5Centavos, long P50Centavos, long P95Centavos);

    public sealed record Resultado(
        IReadOnlyList<BandaDia> Bandas,
        double ProbabilidadeSaldoNegativoEm30Dias,
        int? PrimeiroDiaOffsetP50Negativo);

    public static Resultado Simular(
        IReadOnlyList<long> historicoDeltasDiariosCentavos,
        long saldoAtualCentavos,
        IReadOnlyList<PontoConhecido> fluxoConhecidoFuturo,
        int diasProjecao,
        int seed,
        int numeroDeSimulacoes = 2000,
        int tamanhoDoBloco = 7)
    {
        if (diasProjecao <= 0)
            throw new ArgumentOutOfRangeException(nameof(diasProjecao), "Dias de projeção deve ser positivo.");
        if (numeroDeSimulacoes <= 0)
            throw new ArgumentOutOfRangeException(nameof(numeroDeSimulacoes), "Número de simulações deve ser positivo.");

        var conhecidoPorDia = new long[diasProjecao + 1]; // índice 1..diasProjecao (0 não usado)
        foreach (var ponto in fluxoConhecidoFuturo)
        {
            if (ponto.DiaOffset is >= 1 && ponto.DiaOffset <= diasProjecao)
                conhecidoPorDia[ponto.DiaOffset] += ponto.DeltaCentavos;
        }

        var n = historicoDeltasDiariosCentavos.Count;
        var rng = new Random(seed);

        var saldosPorDia = new List<long>[diasProjecao];
        for (var d = 0; d < diasProjecao; d++) saldosPorDia[d] = new List<long>(numeroDeSimulacoes);

        var horizonteProbabilidade = Math.Min(30, diasProjecao);
        var contagemNegativoNoHorizonte = 0;

        for (var s = 0; s < numeroDeSimulacoes; s++)
        {
            var saldo = saldoAtualCentavos;
            var cruzouNegativoNoHorizonte = false;
            var diaIdx = 0;

            while (diaIdx < diasProjecao)
            {
                var inicioBloco = n > 0 ? rng.Next(0, n) : 0;
                for (var passo = 0; passo < tamanhoDoBloco && diaIdx < diasProjecao; passo++, diaIdx++)
                {
                    var ruido = n > 0 ? historicoDeltasDiariosCentavos[(inicioBloco + passo) % n] : 0L;
                    saldo += ruido + conhecidoPorDia[diaIdx + 1];
                    saldosPorDia[diaIdx].Add(saldo);

                    if (diaIdx < horizonteProbabilidade && saldo < 0) cruzouNegativoNoHorizonte = true;
                }
            }

            if (cruzouNegativoNoHorizonte) contagemNegativoNoHorizonte++;
        }

        var bandas = new List<BandaDia>(diasProjecao);
        for (var d = 0; d < diasProjecao; d++)
        {
            var ordenado = saldosPorDia[d].OrderBy(v => v).ToList();
            bandas.Add(new BandaDia(d + 1, Percentil(ordenado, 0.05), Percentil(ordenado, 0.50), Percentil(ordenado, 0.95)));
        }

        var primeiroDiaP50Negativo = bandas.FirstOrDefault(b => b.P50Centavos < 0)?.DiaOffset;

        return new Resultado(
            bandas,
            (double)contagemNegativoNoHorizonte / numeroDeSimulacoes,
            primeiroDiaP50Negativo);
    }

    /// <summary>
    /// Percentil empírico por interpolação linear entre os dois valores ordenados mais próximos —
    /// o método "tipo 7" de Hyndman &amp; Fan (1996), o DEFAULT do R (<c>quantile()</c>) e do NumPy
    /// (<c>numpy.percentile</c>, <c>method='linear'</c>). Citado explicitamente porque existem ~9
    /// convenções de percentil na literatura que discordam entre si nas pontas da distribuição —
    /// sem nomear qual foi usada, "P5"/"P95" vira ambíguo para quem for auditar o número.
    /// </summary>
    private static long Percentil(IReadOnlyList<long> ordenadoAscendente, double p)
    {
        if (ordenadoAscendente.Count == 0) return 0;
        if (ordenadoAscendente.Count == 1) return ordenadoAscendente[0];

        var posicao = p * (ordenadoAscendente.Count - 1);
        var indiceBaixo = (int)Math.Floor(posicao);
        var indiceAlto = (int)Math.Ceiling(posicao);
        if (indiceBaixo == indiceAlto) return ordenadoAscendente[indiceBaixo];

        var fracao = posicao - indiceBaixo;
        var interpolado = ordenadoAscendente[indiceBaixo] + (ordenadoAscendente[indiceAlto] - ordenadoAscendente[indiceBaixo]) * fracao;
        return (long)Math.Round(interpolado, MidpointRounding.ToEven);
    }
}
