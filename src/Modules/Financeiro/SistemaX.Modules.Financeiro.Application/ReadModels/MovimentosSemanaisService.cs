using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um dia dentro de uma <see cref="SemanaMovimentoResumo"/> — entradas/saídas somadas
/// (nunca com sinal; o gráfico divergente do Bancário desenha as barras em sentidos opostos).</summary>
public sealed record DiaMovimentoResumo(DateOnly Dia, Money Entradas, Money Saidas);

/// <summary>Uma semana (bloco de até 7 dias corridos a partir de <c>inicio</c>) — <see cref="Parcial"/>
/// é <c>true</c> quando o período pedido (<c>fim</c>) corta a semana antes dela completar 7 dias
/// (a "semana em andamento" do mockup — docs/wiring/financeiro-telas-restantes.md §3).</summary>
public sealed record SemanaMovimentoResumo(int Numero, DateOnly Inicio, DateOnly Fim, bool Parcial, IReadOnlyList<DiaMovimentoResumo> Dias);

/// <summary>
/// "Entrou × saiu por semana" (<c>WeeksAnalysisCard</c> do Bancário) — agrega
/// <see cref="MovimentoFinanceiro"/> em baldes de 7 dias corridos a partir de <c>inicio</c>. Puramente
/// determinístico: sem calendário ISO, sem fuso local — cada tenant vê a mesma semana 1 sempre que
/// pedir o mesmo <c>inicio</c>.
/// </summary>
public sealed class MovimentosSemanaisService(IMovimentoFinanceiroRepository movimentos)
{
    public async Task<IReadOnlyList<SemanaMovimentoResumo>> ListarAsync(
        string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var doPeriodo = await movimentos.ListarPorPeriodoAsync(businessId, inicio, fim, ct).ConfigureAwait(false);

        var porDia = doPeriodo
            .GroupBy(m => m.DataMovimento.ParaDateOnly())
            .ToDictionary(
                g => g.Key,
                g => (
                    Entradas: g.Where(m => m.Tipo == TipoMovimentoFinanceiro.Entrada).Aggregate(Money.Zero, (acumulado, m) => acumulado + m.Valor),
                    Saidas: g.Where(m => m.Tipo == TipoMovimentoFinanceiro.Saida).Aggregate(Money.Zero, (acumulado, m) => acumulado + m.Valor)));

        var inicioData = inicio.ParaDateOnly();
        var fimData = fim.ParaDateOnly();
        if (fimData < inicioData) return [];

        var semanas = new List<SemanaMovimentoResumo>();
        var cursor = inicioData;
        var numero = 1;

        while (cursor <= fimData)
        {
            var fimIdeal = cursor.AddDays(6);
            var fimReal = fimIdeal > fimData ? fimData : fimIdeal;

            var dias = new List<DiaMovimentoResumo>();
            for (var dia = cursor; dia <= fimReal; dia = dia.AddDays(1))
            {
                var (entradas, saidas) = porDia.TryGetValue(dia, out var valores) ? valores : (Money.Zero, Money.Zero);
                dias.Add(new DiaMovimentoResumo(dia, entradas, saidas));
            }

            semanas.Add(new SemanaMovimentoResumo(numero, cursor, fimReal, fimReal < fimIdeal, dias));
            cursor = fimIdeal.AddDays(1);
            numero++;
        }

        return semanas;
    }
}
