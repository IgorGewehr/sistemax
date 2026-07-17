using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record PontoDeEquilibrioResultado(
    long CustosFixosMensaisCentavos,
    double MargemContribuicaoPercentual,
    long ReceitaNecessariaMensalCentavos,
    long ReceitaNecessariaDiariaCentavos,
    long ReceitaAcumuladaNoMesCentavos,
    int? DiaDoEquilibrio,
    bool JaAtingiuNoMes);

/// <summary>
/// Orquestra <see cref="BreakevenMensal"/> sobre os ports reais — catálogo #7 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005).
///
/// INSUMOS:
/// - Custos fixos mensais: soma das <see cref="RecorrenciaAgg"/> ATIVAS do tipo
///   <see cref="TipoContaRecorrente.APagar"/> — o "aluguel, salário, contas fixas" já MARCADO no
///   sistema (docs/financeiro-features.md §4.12), normalizado para equivalente MENSAL pela
///   frequência de cada uma (ex.: uma recorrência trimestral de R$300 vira R$100/mês).
/// - Margem de contribuição percentual: agregada de <c>fato_margem_produto</c> (#6) na JANELA de
///   <see cref="DiasDeJanelaParaMc"/> dias — MC% = (Σreceita − Σcusto) / Σreceita da janela. Sem
///   nenhuma venda de produto controlado por estoque na janela (Σreceita = 0), MC% cai para 0 —
///   documentado: sem dado, o motor não inventa margem.
/// - Receita diária do mês corrente: <c>fato_receita_diaria</c> do 1º dia do mês até hoje.
/// </summary>
public sealed class PontoDeEquilibrioService(
    IRecorrenciaRepository recorrencias,
    IFatoMargemProdutoRepository fatoMargemProduto,
    IFatoReceitaDiariaRepository fatoReceitaDiaria,
    IRelogio relogio)
{
    private const int DiasDeJanelaParaMc = 30;

    public async Task<PontoDeEquilibrioResultado> CalcularAsync(string businessId, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.Agora().UtcDateTime);
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);
        var diasNoMes = DateTime.DaysInMonth(hoje.Year, hoje.Month);

        var custosFixosMensais = await CalcularCustosFixosMensaisAsync(businessId, ct).ConfigureAwait(false);
        var mcPct = await CalcularMargemContribuicaoPercentualAsync(businessId, hoje, ct).ConfigureAwait(false);

        var receitaDiaria = await fatoReceitaDiaria.ListarAsync(businessId, inicioDoMes, hoje, ct).ConfigureAwait(false);
        var pontos = receitaDiaria
            .OrderBy(f => f.Dia)
            .Select(f => new BreakevenMensal.PontoReceitaDiaria(f.Dia.Day, f.ReceitaCentavos))
            .ToList();

        var resultado = BreakevenMensal.Calcular(custosFixosMensais, mcPct, pontos, diasNoMes);

        return new PontoDeEquilibrioResultado(
            custosFixosMensais,
            mcPct,
            resultado.ReceitaNecessariaMensalCentavos,
            resultado.ReceitaNecessariaDiariaCentavos,
            resultado.ReceitaAcumuladaCentavos,
            resultado.DiaDoEquilibrio,
            resultado.JaAtingiuNoMes);
    }

    private async Task<long> CalcularCustosFixosMensaisAsync(string businessId, CancellationToken ct)
    {
        var ativas = await recorrencias.ListarAtivasAsync(businessId, ct).ConfigureAwait(false);
        return ativas
            .Where(r => r.Tipo == TipoContaRecorrente.APagar)
            .Sum(r => EquivalenteMensal(r));
    }

    /// <summary>Normaliza o valor previsto de uma recorrência para equivalente MENSAL, pela
    /// frequência — fórmula fechada: <c>valor ÷ meses-do-ciclo</c> (semanal usa 52/12 ≈ 4,345
    /// semanas/mês, a conversão padrão de anualização de frequências sub-mensais).</summary>
    private static long EquivalenteMensal(RecorrenciaAgg recorrencia)
    {
        var valor = recorrencia.ValorPrevisto.Centavos;
        return recorrencia.Frequencia switch
        {
            FrequenciaRecorrencia.Semanal => (long)Math.Round(valor * 52.0 / 12.0),
            FrequenciaRecorrencia.Mensal => valor,
            FrequenciaRecorrencia.Bimestral => (long)Math.Round(valor / 2.0),
            FrequenciaRecorrencia.Trimestral => (long)Math.Round(valor / 3.0),
            FrequenciaRecorrencia.Semestral => (long)Math.Round(valor / 6.0),
            FrequenciaRecorrencia.Anual => (long)Math.Round(valor / 12.0),
            _ => throw new ArgumentOutOfRangeException(nameof(recorrencia), recorrencia.Frequencia, "Frequência de recorrência desconhecida."),
        };
    }

    private async Task<double> CalcularMargemContribuicaoPercentualAsync(string businessId, DateOnly hoje, CancellationToken ct)
    {
        var janela = await fatoMargemProduto.ListarAsync(businessId, hoje.AddDays(-DiasDeJanelaParaMc), hoje, ct).ConfigureAwait(false);
        var receitaTotal = janela.Sum(f => f.ReceitaCentavos);
        var custoTotal = janela.Sum(f => f.CustoCentavos);

        return receitaTotal > 0 ? (double)(receitaTotal - custoTotal) / receitaTotal : 0;
    }
}
