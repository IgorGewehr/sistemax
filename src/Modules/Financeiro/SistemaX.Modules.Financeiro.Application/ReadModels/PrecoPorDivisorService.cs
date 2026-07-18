using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record PrecoPorDivisorResultado(
    long PrecoSugeridoCentavos, long PrecoPisoCentavos, decimal SomaPercentuaisSobrePreco,
    decimal? MargemRealNoPrecoAtualPercent, decimal? MdrPercent, double? AliquotaEfetivaPercent, decimal ComissaoPercent);

/// <summary>
/// PREÇO POR DIVISOR (matemonstro idéia 2, docs/financeiro/ideias-matemonstro.md) orquestrado
/// sobre os PORTS reais — o LAR ÚNICO da fórmula é <see cref="PrecoPorDivisor"/> (Quant); este
/// read-model só RESOLVE os %-sobre-preço a partir do que já existe, nunca recalcula taxa
/// nenhuma:
///
/// - MDR → <c>FormaDePagamento.TaxaPercentual</c> (opcional: sem <c>formaDePagamentoId</c>, MDR
///   entra como 0 — venda em dinheiro/PIX sem taxa, por exemplo);
/// - alíquota efetiva do Simples → <see cref="RadarDoSimplesService"/> (mesmo mix real do tenant
///   que o Radar já calcula — opcional via <paramref name="incluirAliquotaEfetiva"/>, para o dono
///   que já embute imposto no preço de tabela por outra via);
/// - comissão → parâmetro livre (<paramref name="comissaoPercent"/>) — NÃO existe cadastro de
///   comissão por tenant ainda (mesmo gap documentado em
///   <see cref="ReceitaPorProfissionalService"/>/<c>OsFaturadaHandler</c>); quando esse cadastro
///   existir, este método ganha um overload que o resolve automaticamente por técnico.
///
/// Serve os 3 clusters de %-sobre-preço dos verticais MEI-alvo (vestuário/marketplace, delivery,
/// beleza/comissão) com a MESMA fórmula — nenhum "if vertical" aqui, cada um só entra com um
/// <c>comissaoPercent</c>/forma de pagamento diferente.
/// </summary>
public sealed class PrecoPorDivisorService(IFormaDePagamentoRepository formasDePagamento, RadarDoSimplesService radarDoSimples)
{
    public async Task<PrecoPorDivisorResultado?> CalcularAsync(
        string businessId, long custoCentavos, decimal margemDesejadaPercent,
        string? formaDePagamentoId = null, decimal comissaoPercent = 0m, bool incluirAliquotaEfetiva = true,
        long? precoAtualCentavos = null, CancellationToken ct = default)
    {
        decimal? mdrPercent = null;
        if (formaDePagamentoId is not null)
        {
            var forma = await formasDePagamento.ObterPorIdAsync(businessId, formaDePagamentoId, ct).ConfigureAwait(false);
            mdrPercent = forma?.TaxaPercentual;
        }

        double? aliquotaEfetivaPercent = null;
        if (incluirAliquotaEfetiva)
        {
            var radar = await radarDoSimples.CalcularAsync(businessId, anexo: null, ct).ConfigureAwait(false);
            if (radar.Sucesso) aliquotaEfetivaPercent = radar.Valor.AliquotaEfetiva;
        }

        var percentuais = new List<decimal>();
        if (mdrPercent is { } mdr) percentuais.Add(mdr);
        if (aliquotaEfetivaPercent is { } aliquota) percentuais.Add((decimal)aliquota);
        if (comissaoPercent > 0) percentuais.Add(comissaoPercent);

        var resultado = PrecoPorDivisor.Calcular(custoCentavos, percentuais, margemDesejadaPercent, precoAtualCentavos);
        if (resultado is null) return null;

        return new PrecoPorDivisorResultado(
            resultado.PrecoSugeridoCentavos, resultado.PrecoPisoCentavos, resultado.SomaPercentuaisSobrePreco,
            resultado.MargemRealNoPrecoAtualPercent, mdrPercent, aliquotaEfetivaPercent, comissaoPercent);
    }
}
