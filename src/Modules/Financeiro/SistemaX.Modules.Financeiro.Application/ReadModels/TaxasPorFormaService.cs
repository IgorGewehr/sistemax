using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma linha do painel "Ver por forma" — <see cref="Volume"/> é a soma bruta recebida
/// nessa forma no período; <see cref="Taxa"/> é o MDR já descontado sobre esse volume
/// (<c>FormaDePagamento.CalcularTaxa</c>, o mesmo cálculo que <c>FatoRecebiveisProjection</c>
/// usa).</summary>
public sealed record TaxaPorFormaResumo(string FormaPagamentoId, string Forma, Money Volume, decimal TaxaPercentual, Money Taxa);

public sealed record TaxasPorFormaResumo(Money TaxaTotal, Money VolumeTotal, decimal PercentualVolume, IReadOnlyList<TaxaPorFormaResumo> PorForma);

/// <summary>
/// O painel "Ver por forma" do Super Consultor Bancário (docs/wiring/financeiro-telas-restantes.md
/// §3): quanto de MDR o negócio pagou no período, quebrado por forma de pagamento — só sobre
/// ENTRADAS (dinheiro recebido; saídas não têm taxa de maquininha). <see cref="FormaDePagamento.TaxaPercentual"/>
/// é o LAR ÚNICO da taxa — nenhum número hardcoded aqui.
/// </summary>
public sealed class TaxasPorFormaService(IMovimentoFinanceiroRepository movimentos, IFormaDePagamentoRepository formas)
{
    public async Task<TaxasPorFormaResumo> CalcularAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default)
    {
        var entradasDoPeriodo = (await movimentos.ListarPorPeriodoAsync(businessId, inicio, fim, ct).ConfigureAwait(false))
            .Where(m => m.Tipo == TipoMovimentoFinanceiro.Entrada && !m.EhEstorno)
            .ToList();

        var porForma = new List<TaxaPorFormaResumo>();
        var taxaTotal = Money.Zero;
        var volumeTotal = Money.Zero;

        foreach (var grupo in entradasDoPeriodo.GroupBy(m => m.FormaPagamentoId))
        {
            var volume = grupo.Aggregate(Money.Zero, (acumulado, m) => acumulado + m.Valor);
            var forma = await formas.ObterPorIdAsync(businessId, grupo.Key, ct).ConfigureAwait(false);
            var nome = forma?.Nome ?? grupo.Key;
            var taxaPercentual = forma?.TaxaPercentual ?? 0m;
            var taxa = forma?.CalcularTaxa(volume) ?? Money.Zero;

            porForma.Add(new TaxaPorFormaResumo(grupo.Key, nome, volume, taxaPercentual, taxa));
            taxaTotal += taxa;
            volumeTotal += volume;
        }

        var percentualVolume = volumeTotal.EhZero ? 0m : Math.Round(taxaTotal.EmReais / volumeTotal.EmReais * 100m, 2);

        return new TaxasPorFormaResumo(
            taxaTotal, volumeTotal, percentualVolume,
            porForma.OrderByDescending(p => p.Volume.Centavos).ToList());
    }
}
