namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Espalha um total exato (centavos) em N competências (meses) — o "cronograma linear" que é o LAR
/// ÚNICO de "espalhar valor no tempo" no Financeiro (docs/financeiro/design-analise-por-projeto.md
/// §4.2 e docs/financeiro/design-imobilizado-roi.md §10): RECEITA DIFERIDA (P1-5,
/// docs/financeiro/revisao-domain-fit-cnpj.md — cobrança de assinatura de ciclo &gt; mensal
/// reconhecida pró-rata), amortização de <c>AtivoAmortizavel</c>/<c>AtivoDeCapital</c> (custo de
/// projeto/imobilizado, ainda não implementado) e depreciação linear TODOS devem reusar este
/// helper — nunca reimplementar o rateio por fora.
///
/// Constrói sobre <see cref="RateioProporcional"/> (Hamilton/maior resto) com pesos uniformes
/// (<c>1×meses</c>): <c>Σ valores == totalCentavos</c> SEMPRE, por construção — nenhum centavo
/// perdido ou sobrando por arredondamento. Com restos iguais, o desempate de
/// <see cref="RateioProporcional.Alocar"/> (<c>ThenBy(índice)</c>) dá o centavo extra às PRIMEIRAS
/// competências do cronograma.
///
/// Determinístico e puro: não persiste nada (convenção do repo — totais/derivados são computados
/// da fonte, nunca cacheados). Todo leitor (DRE, painel, cron de reconhecimento) recomputa
/// <see cref="Gerar"/> a partir do agregado de origem; um cron atrasado nunca produz um número
/// errado, só atrasa quando o rastro contábil é gravado.
/// </summary>
public static class CronogramaLinear
{
    /// <summary>
    /// Gera <paramref name="meses"/> pares (competência, valor) cobrindo <paramref name="totalCentavos"/>
    /// a partir de <paramref name="inicio"/> (o dia é ignorado — cronograma tem granularidade de
    /// MÊS; a competência de cada parcela é sempre o dia 1 do mês correspondente).
    /// </summary>
    public static IReadOnlyList<(DateOnly Competencia, long ValorCentavos)> Gerar(long totalCentavos, int meses, DateOnly inicio)
    {
        if (meses <= 0)
            throw new ArgumentOutOfRangeException(nameof(meses), meses, "Cronograma linear precisa de ao menos 1 competência.");

        var pesosUniformes = Enumerable.Repeat(1L, meses).ToList();
        var valores = RateioProporcional.Alocar(totalCentavos, pesosUniformes);

        var primeiraCompetencia = new DateOnly(inicio.Year, inicio.Month, 1);
        var resultado = new List<(DateOnly, long)>(meses);
        for (var i = 0; i < meses; i++)
        {
            resultado.Add((primeiraCompetencia.AddMonths(i), valores[i]));
        }
        return resultado;
    }
}
