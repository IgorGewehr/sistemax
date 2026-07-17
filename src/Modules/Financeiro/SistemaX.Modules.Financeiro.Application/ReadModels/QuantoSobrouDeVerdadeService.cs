using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>
/// "Quanto sobrou de verdade" (docs/financeiro-features.md §4.17) — não é o lucro do DRE
/// (competência) nem o saldo bruto de caixa (que pode incluir dinheiro que já tem dono): é quanto
/// dá pra tirar da empresa hoje sem sufocar o caixa amanhã.
///
/// Fórmula do MVP, REDUZIDA em relação à spec completa: <c>Saldo em caixa − contas a pagar nos
/// próximos N dias</c>. A spec completa também subtrai imposto (DAS/MEI) reservado e parcela de
/// dívida/empréstimo — esses dados vivem em entidades (<c>DasRecord</c>, <c>Employee</c>) que
/// pertencem a outros módulos (Fiscal, RH) ainda não construídos nesta partição; a fórmula será
/// estendida quando esses módulos existirem e expuserem os dados via evento/port.
/// </summary>
public sealed record QuantoSobrouResultado(Money SaldoEmCaixa, Money JaTemDono, Money PodeTirar);

public sealed class QuantoSobrouDeVerdadeService(
    IMovimentoFinanceiroRepository movimentos, IContaAPagarRepository contasAPagar, IRelogio relogio)
{
    private const int HorizonteDiasContasAPagar = 30;

    public async Task<QuantoSobrouResultado> CalcularAsync(string businessId, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var saldoEmCaixa = await movimentos.CalcularSaldoAsync(businessId, null, agora, ct);

        var contasProximosDias = await contasAPagar.ListarAbertasAteAsync(businessId, agora.AddDays(HorizonteDiasContasAPagar), ct);
        var jaTemDono = contasProximosDias
            .SelectMany(c => c.Parcelas)
            .Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)
            .Aggregate(Money.Zero, (acumulado, p) => acumulado + (p.Valor - p.ValorPago));

        var podeTirar = saldoEmCaixa - jaTemDono;
        return new QuantoSobrouResultado(saldoEmCaixa, jaTemDono, podeTirar.EhNegativo ? Money.Zero : podeTirar);
    }
}
