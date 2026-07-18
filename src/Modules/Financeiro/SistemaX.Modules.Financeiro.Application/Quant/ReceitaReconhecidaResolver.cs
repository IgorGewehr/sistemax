using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

namespace SistemaX.Modules.Financeiro.Application.Quant;

/// <summary>
/// P1-5 (docs/financeiro/revisao-domain-fit-cnpj.md) — separa CAIXA (a <c>ContaAReceber</c> nasce
/// no valor CHEIO, na competência da cobrança — é o recebível, intocado) de COMPETÊNCIA (quanto
/// desse valor é reconhecido no DRE de um mês específico). Toda conta SEM
/// <see cref="ContaFinanceiraBase.MesesDeReconhecimento"/> reconhece 100% na própria
/// <c>DataCompetencia</c> — o comportamento de sempre, intacto para venda/OS/lançamento manual e
/// para assinatura de ciclo mensal. Contas COM reconhecimento diferido (hoje só cobrança de
/// assinatura de ciclo trimestral/semestral/anual — <c>Assinatura.GerarCobranca</c>) espalham o
/// valor via <see cref="CronogramaLinear"/> (Hamilton, centavo-exato) e devolvem só a fração cujo
/// mês cai na janela pedida.
///
/// Σ sobre TODAS as competências do cronograma de uma conta sempre bate com
/// <see cref="ContaFinanceiraBase.ValorTotal"/> (conservação de centavos — mesma garantia de
/// <see cref="RateioProporcional"/>): reconhecer a mesma conta em 12 chamadas com competências
/// diferentes, uma por mês, nunca perde nem duplica centavo.
/// </summary>
public static class ReceitaReconhecidaResolver
{
    /// <summary>Maior ciclo de reconhecimento suportado hoje (Anual = 12 meses) — o quanto o
    /// leitor do DRE precisa olhar PRA TRÁS de <c>inicioJanela</c> para não perder cobranças cujo
    /// cronograma de reconhecimento ainda não terminou.</summary>
    public const int MaiorHorizonteDeReconhecimentoEmMeses = 12;

    /// <summary>Centavos de <paramref name="conta"/> reconhecidos na janela [<paramref name="inicioJanela"/>,
    /// <paramref name="fimJanela"/>] — INCLUSIVA nas duas pontas (mesma convenção de
    /// <c>IContaAReceberRepository.ListarPorCompetenciaAsync</c>).</summary>
    public static long CentavosNaJanela(ContaFinanceiraBase conta, DateTimeOffset inicioJanela, DateTimeOffset fimJanela)
    {
        if (conta.MesesDeReconhecimento is not { } meses)
            return EstaNaJanela(conta.DataCompetencia, inicioJanela, fimJanela) ? conta.ValorTotal.Centavos : 0;

        var inicioCronograma = new DateOnly(conta.DataCompetencia.Year, conta.DataCompetencia.Month, 1);
        var cronograma = CronogramaLinear.Gerar(conta.ValorTotal.Centavos, meses, inicioCronograma);

        long total = 0;
        foreach (var (competencia, valor) in cronograma)
        {
            var dataCompetencia = new DateTimeOffset(competencia.Year, competencia.Month, 1, 0, 0, 0, inicioJanela.Offset);
            if (EstaNaJanela(dataCompetencia, inicioJanela, fimJanela)) total += valor;
        }
        return total;
    }

    private static bool EstaNaJanela(DateTimeOffset data, DateTimeOffset inicioJanela, DateTimeOffset fimJanela)
        => data >= inicioJanela && data <= fimJanela;
}
