namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// Runway (catálogo #2 do plano de inteligência do Financeiro — docs/financeiro/
/// inteligencia-arquitetura.md §4/ADR-0005) — "quantos dias eu aguento se parar de vender?". Duas
/// leituras, deliberadamente diferentes (o doc pede as duas, não uma média entre elas):
///
/// - <b>Bruto</b>: <c>saldo ÷ burn diário EWMA</c> — pessimista de propósito, ignora toda receita
///   futura (mesmo a já certa: parcelas a receber já emitidas). É o "pior caso simples".
/// - <b>Realista</b>: primeiro dia em que a banda P50 de <see cref="BandasDeFluxoDeCaixa"/> cruza
///   negativo — já incorpora recebíveis/pagáveis agendados e o padrão histórico de entradas, não
///   só as saídas.
/// </summary>
public static class RunwayCalculator
{
    public sealed record Resultado(int? DiasRunwayBruto, int? DiasRunwayRealista);

    /// <summary>
    /// EWMA (média móvel exponencialmente ponderada) do "burn" diário: só a fração NEGATIVA de cada
    /// dia entra na média (dia de caixa positivo conta burn ZERO, nunca "burn negativo") —
    /// <c>b_t = max(0, −delta_t)</c>, <c>E_t = α·b_t + (1−α)·E_{t−1}</c>, <c>E_0 = b_0</c>, com
    /// <c>α = 2 / (janela + 1)</c> — a mesma conversão janela→α de uma EMA financeira clássica
    /// (janela=14 ⇒ α≈0,133, meia-vida ≈9,4 dias).
    /// </summary>
    public static double CalcularBurnDiarioEwma(IReadOnlyList<long> deltasDiariosCentavosOrdenadoCronologicamente, int janela = 14)
    {
        if (deltasDiariosCentavosOrdenadoCronologicamente.Count == 0) return 0;
        if (janela <= 0) throw new ArgumentOutOfRangeException(nameof(janela), "Janela deve ser positiva.");

        var alfa = 2.0 / (janela + 1);
        double ewma = Math.Max(0, -deltasDiariosCentavosOrdenadoCronologicamente[0]);

        for (var i = 1; i < deltasDiariosCentavosOrdenadoCronologicamente.Count; i++)
        {
            var burnDoDia = Math.Max(0, -deltasDiariosCentavosOrdenadoCronologicamente[i]);
            ewma = alfa * burnDoDia + (1 - alfa) * ewma;
        }

        return ewma;
    }

    /// <summary>Combina o runway bruto (derivado aqui) com o realista (já calculado por
    /// <see cref="BandasDeFluxoDeCaixa"/> — <c>PrimeiroDiaOffsetP50Negativo</c> é passado direto).
    /// Nenhum dos dois é negativo: saldo já negativo hoje é "0 dias de runway", não um número
    /// negativo sem sentido de leitura.</summary>
    public static Resultado Calcular(long saldoAtualCentavos, double burnDiarioEwmaCentavos, int? primeiroDiaP50NegativoOffset)
    {
        int? bruto = burnDiarioEwmaCentavos > 0
            ? Math.Max(0, (int)Math.Floor(saldoAtualCentavos / burnDiarioEwmaCentavos))
            : null; // sem burn médio positivo: não há "queima" no ritmo atual — runway bruto é infinito (null)

        var realista = primeiroDiaP50NegativoOffset is { } dia ? Math.Max(0, dia) : (int?)null;

        return new Resultado(bruto, realista);
    }
}
