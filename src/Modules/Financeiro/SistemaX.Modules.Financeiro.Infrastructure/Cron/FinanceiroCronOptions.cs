namespace SistemaX.Modules.Financeiro.Infrastructure.Cron;

/// <summary>
/// Configuração dos jobs de background do Financeiro — ver
/// <see cref="AvaliarParcelasVencidasBackgroundService"/>. Seção <c>Financeiro:Cron</c> em
/// <c>config.json</c> (mesmo mecanismo de binding de <c>LocalDatabaseOptions</c> em
/// Infrastructure.Local); ausente, usa os defaults abaixo.
/// </summary>
public sealed class FinanceiroCronOptions
{
    public const string SectionName = "Financeiro:Cron";

    /// <summary>
    /// Intervalo entre rodadas do "cron financeiro" que marca parcelas vencidas
    /// (<c>AvaliarParcelasVencidasUseCase</c>, docs/financeiro-datamodel.md §4.2). Default curto (1
    /// min) porque o caso de uso é barato quando não há parcela nova cruzando o vencimento — mesmo
    /// racional do default de <c>ProjectionCatchUpInterval</c>.
    /// </summary>
    public TimeSpan IntervaloAvaliacaoParcelasVencidas { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Intervalo entre rodadas do cron que fatura assinaturas/recorrências vencidas
    /// (<see cref="FaturarAssinaturasBackgroundService"/>, P0-3 — docs/financeiro/revisao-domain-fit-cnpj.md).
    /// Default mais longo (1h) que o de parcelas vencidas: cobrança de ciclo (mensal na melhor das
    /// hipóteses) não precisa da mesma cadência de 1min — o catch-up no boot cobre qualquer atraso
    /// entre rodadas.
    /// </summary>
    public TimeSpan IntervaloFaturamentoAssinaturas { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — dias de GRAÇA após uma assinatura
    /// entrar em <c>StatusAssinatura.Inadimplente</c> (cobrança vencida sem pagamento) antes de o
    /// dunning virar churn (<c>AvaliarDunningAssinaturasUseCase</c>, rodado no mesmo ciclo de
    /// <see cref="FaturarAssinaturasBackgroundService"/>). Regularizar a cobrança
    /// (<c>ParcelaBaixada</c>) a qualquer momento dentro da graça cancela o relógio.
    /// </summary>
    public int DiasGracaInadimplenciaAssinatura { get; set; } = 7;
}
