using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record ResumoFaixaDeAtrasoResultado(FaixaDeAtraso Faixa, long ValorCentavos, long ProvisaoCentavos, int Quantidade);

public sealed record InadimplenciaResultado(
    long ValorTotalEmAbertoCentavos,
    long ProvisaoEsperadaCentavos,
    long ValorLiquidoEsperadoCentavos,
    IReadOnlyList<ResumoFaixaDeAtrasoResultado> PorFaixa);

/// <summary>
/// Orquestra <see cref="InadimplenciaRollRate"/> sobre <see cref="IContaAReceberRepository"/> —
/// catálogo #3 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
/// ADR-0005): "esse 'a receber' vale quanto de verdade?".
///
/// INSUMO: TODAS as parcelas em aberto/parcial/atrasada de <c>ContaAReceber</c> — não só as que já
/// venceram (uma parcela a vencer é <see cref="FaixaDeAtraso.EmDia"/>, taxa de perda 0%, mas ainda
/// entra na base para o "valor total em aberto" fazer sentido). <c>ListarAbertasAteAsync</c> exige
/// uma data-limite; usamos <see cref="HorizonteDistanteParaCapturarTudo"/> (5 anos à frente) —
/// suficiente pra qualquer parcelamento real deste ERP, documentado em vez de introduzir um método
/// novo de porta só pra "sem limite".
/// </summary>
public sealed class InadimplenciaService(IContaAReceberRepository contasAReceber, IRelogio relogio)
{
    private static readonly TimeSpan HorizonteDistanteParaCapturarTudo = TimeSpan.FromDays(365 * 5);

    public async Task<InadimplenciaResultado> CalcularAsync(string businessId, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var contas = await contasAReceber.ListarAbertasAteAsync(businessId, agora + HorizonteDistanteParaCapturarTudo, ct).ConfigureAwait(false);

        var parcelas = contas
            .SelectMany(c => c.Parcelas)
            .Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)
            .Select(p => new InadimplenciaRollRate.ParcelaEmAberto(
                p.Id,
                (p.Valor - p.ValorPago).Centavos,
                DiasAtraso: (int)(agora - p.Vencimento).TotalDays))
            .ToList();

        var resultado = InadimplenciaRollRate.CalcularProvisao(parcelas);

        var porFaixa = resultado.PorFaixa
            .Select(kv => new ResumoFaixaDeAtrasoResultado(kv.Key, kv.Value.ValorCentavos, kv.Value.ProvisaoCentavos, kv.Value.Quantidade))
            .OrderBy(r => r.Faixa)
            .ToList();

        return new InadimplenciaResultado(
            resultado.ValorTotalEmAbertoCentavos,
            resultado.ProvisaoEsperadaCentavos,
            resultado.ValorTotalEmAbertoCentavos - resultado.ProvisaoEsperadaCentavos,
            porFaixa);
    }
}
