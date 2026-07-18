using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Mrr;

/// <summary>
/// Decomposição da variação do MRR num mês em <see cref="TipoMovimentoMrr"/> (P1-4,
/// docs/financeiro/revisao-domain-fit-cnpj.md) — a lente CORRETA e auditável, em contraste com
/// <c>ReceitaRecorrenteService</c> (que deriva "novo"/"churn" por álgebra sobre um SNAPSHOT do MRR
/// atual: <c>mrrInicioMes = mrr - novo + churn</c>, viés documentado — uma assinatura nascida E
/// cancelada no MESMO mês entra no churn sem nunca ter entrado no "início do mês", inflando
/// artificialmente o churn% de quem olha só a álgebra).
///
/// Aqui <see cref="MrrInicio"/> é a soma CUMULATIVA de todos os movimentos ANTES da competência —
/// por construção (ver <see cref="MovimentoMrr"/>), é EXATAMENTE o MRR real no primeiro instante do
/// mês, nunca uma reconstrução por trás. Uma assinatura nascida-e-cancelada no mesmo mês aparece em
/// AMBOS Novo e Churn (honestidade: é um evento bruto real), mas nunca contamina <see cref="MrrInicio"/>
/// (que não a inclui) — o <see cref="ChurnPercent"/> resultante mede perda sobre a base que
/// REALMENTE existia no início do mês, sem o viés.
/// </summary>
public sealed record MovimentoMrrResumoMensal(
    DateOnly Competencia, Money MrrInicio, Money Novo, Money Expansao, Money Contracao, Money Churn, Money Reativacao,
    Money MrrFim, decimal ChurnPercent);

public sealed class PainelDeMovimentosMrrService(IMovimentoMrrRepository movimentos)
{
    public async Task<MovimentoMrrResumoMensal> CalcularAsync(string businessId, DateOnly competencia, CancellationToken ct = default)
    {
        var todos = await movimentos.ListarAsync(businessId, ct).ConfigureAwait(false);

        var mrrInicio = SomarComSinal(todos.Where(m => m.Competencia < competencia));

        var doMes = todos.Where(m => m.Competencia == competencia).ToList();
        var novo = SomarPorTipo(doMes, TipoMovimentoMrr.Novo);
        var expansao = SomarPorTipo(doMes, TipoMovimentoMrr.Expansao);
        var contracao = SomarPorTipo(doMes, TipoMovimentoMrr.Contracao);
        var churn = SomarPorTipo(doMes, TipoMovimentoMrr.Churn);
        var reativacao = SomarPorTipo(doMes, TipoMovimentoMrr.Reativacao);

        // A IDENTIDADE que P1-4 pede testada — construída aqui termo a termo, não um atalho.
        var mrrFim = mrrInicio + novo + expansao - contracao - churn + reativacao;

        var churnPercent = mrrInicio <= 0 ? 0m : Math.Round((decimal)churn / mrrInicio * 100m, 1);

        return new MovimentoMrrResumoMensal(
            competencia, new Money(mrrInicio), new Money(novo), new Money(expansao), new Money(contracao),
            new Money(churn), new Money(reativacao), new Money(mrrFim), churnPercent);
    }

    private static long SomarPorTipo(IReadOnlyList<MovimentoMrr> doMes, TipoMovimentoMrr tipo)
        => doMes.Where(m => m.Tipo == tipo).Sum(m => m.ValorCentavos);

    private static long SomarComSinal(IEnumerable<MovimentoMrr> movimentos) => movimentos.Sum(m => m.Tipo switch
    {
        TipoMovimentoMrr.Novo or TipoMovimentoMrr.Expansao or TipoMovimentoMrr.Reativacao => m.ValorCentavos,
        TipoMovimentoMrr.Contracao or TipoMovimentoMrr.Churn => -m.ValorCentavos,
        _ => 0
    });
}
