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
    bool JaAtingiuNoMes,
    double? MargemDeSegurancaPercentual,
    double? Gao,
    long ReceitaNecessariaMensalEconomicaCentavos);

/// <summary>
/// Orquestra <see cref="BreakevenMensal"/> sobre os ports reais — catálogo #7 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005).
///
/// INSUMOS:
/// - Custos fixos mensais: soma das <see cref="RecorrenciaAgg"/> ATIVAS do tipo
///   <see cref="TipoContaRecorrente.APagar"/> — o "aluguel, salário, contas fixas" já MARCADO no
///   sistema (docs/financeiro-features.md §4.12), normalizado para equivalente MENSAL pela
///   frequência de cada uma (ex.: uma recorrência trimestral de R$300 vira R$100/mês).
/// - Margem de contribuição percentual — BLENDED POR MIX (P1-2, docs/financeiro/revisao-domain-fit-cnpj.md):
///   ver <see cref="CalcularMargemContribuicaoPercentualAsync"/>.
/// - Receita diária do mês corrente: <c>fato_receita_diaria</c> do 1º dia do mês até hoje.
/// - Custo de oportunidade mensal (PE econômico, ideia 1 do matemonstro): 0 por padrão — sem
///   config de taxa de desconto cadastrada (painel de ROI/imobilizado ainda não existe), o PE
///   econômico degrada para o PE contábil (mesma regra documentada em <see cref="BreakevenMensal"/>).
/// </summary>
public sealed class PontoDeEquilibrioService(
    IRecorrenciaRepository recorrencias,
    IFatoReceitaDiariaRepository fatoReceitaDiaria,
    DreGerencialService dreGerencial,
    IRelogio relogio)
{
    private const int DiasDeJanelaParaMc = 30;

    public async Task<PontoDeEquilibrioResultado> CalcularAsync(
        string businessId, long custoDeOportunidadeMensalCentavos = 0, CancellationToken ct = default)
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

        var resultado = BreakevenMensal.Calcular(custosFixosMensais, mcPct, pontos, diasNoMes, custoDeOportunidadeMensalCentavos);

        return new PontoDeEquilibrioResultado(
            custosFixosMensais,
            mcPct,
            resultado.ReceitaNecessariaMensalCentavos,
            resultado.ReceitaNecessariaDiariaCentavos,
            resultado.ReceitaAcumuladaCentavos,
            resultado.DiaDoEquilibrio,
            resultado.JaAtingiuNoMes,
            resultado.MargemDeSegurancaPercentual,
            resultado.Gao,
            resultado.ReceitaNecessariaMensalEconomicaCentavos);
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

    /// <summary>
    /// P1-2 (docs/financeiro/revisao-domain-fit-cnpj.md) — MC% BLENDED POR MIX, não mais a MC% de
    /// uma população só (o antigo cálculo usava <c>fato_margem_produto</c>, que só existe para
    /// venda avulsa de produto controlado por estoque — ou seja, só a corrente Comercio — aplicada
    /// sobre a receita TOTAL, incluindo Servico/Recorrente que nem apareciam no numerador).
    ///
    /// FÓRMULA: cada corrente <c>s</c> tem sua própria margem de contribuição percentual <c>MC%_s =
    /// M_s ÷ R_s</c> (receita bruta reconhecida menos custo direto da própria corrente — CMV real
    /// para Comercio, comissão para Servico, ~0 para Recorrente — exatamente o que
    /// <see cref="DreGerencialService.CalcularPorCorrente"/> já devolve, reusado aqui sem duplicar
    /// lógica). A MC% do MIX é a média das MC%_s PONDERADA pela participação de receita de cada
    /// corrente na janela: <c>MC%_mix = Σ_s w_s · MC%_s</c>, com <c>w_s = R_s ÷ R_total</c>.
    ///
    /// IDENTIDADE que simplifica a implementação: substituindo <c>w_s</c> e <c>MC%_s</c>,
    /// <c>Σ_s (R_s ÷ R_total) · (M_s ÷ R_s) = (Σ_s M_s) ÷ R_total = M_total ÷ R_total</c> — os
    /// <c>R_s</c> individuais se cancelam. Ou seja, "blended por mix" não precisa calcular
    /// MC%_s corrente a corrente e depois ponderar: BASTA somar a margem (já calculada com o custo
    /// PRÓPRIO de cada corrente) e dividir pela receita total. É essa soma agregada — nunca a MC%
    /// de uma corrente isolada aplicada ao total — que corrige a distorção do P1-2: antes, usar a
    /// MC% baixa de Comercio sobre receita que inclui Servico/Recorrente (MC% alta) SUPERESTIMA a
    /// receita necessária; usar a MC% alta de Recorrente sobre tudo SUBESTIMARIA. A soma agregada
    /// (M_total/R_total) sempre reflete a composição real do mix, qualquer que ela seja no mês.
    ///
    /// Sem receita nenhuma na janela (R_total = 0), MC% cai para 0 — mesmo racional de sempre: sem
    /// dado, o motor não inventa margem.
    /// </summary>
    private async Task<double> CalcularMargemContribuicaoPercentualAsync(string businessId, DateOnly hoje, CancellationToken ct)
    {
        var inicioJanela = new DateTimeOffset(hoje.AddDays(-DiasDeJanelaParaMc).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var fimJanela = new DateTimeOffset(hoje.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var dre = await dreGerencial.CalcularAsync(businessId, inicioJanela, fimJanela, ct).ConfigureAwait(false);

        var receitaTotal = dre.PorCorrente.Sum(p => p.ReceitaBruta.Centavos);
        var margemTotal = dre.PorCorrente.Sum(p => p.Margem.Centavos);

        return receitaTotal > 0 ? (double)margemTotal / receitaTotal : 0;
    }
}
