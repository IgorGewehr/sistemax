namespace SistemaX.Modules.Fiscal.Infrastructure.Cron;

/// <summary>
/// Configuração do job de retransmissão fiscal — ver
/// <see cref="RetransmissaoFiscalBackgroundService"/>. Seção <c>Fiscal:Cron</c> em
/// <c>config.json</c>; ausente, usa os defaults abaixo.
/// </summary>
public sealed class FiscalCronOptions
{
    public const string SectionName = "Fiscal:Cron";

    /// <summary>Intervalo entre rodadas do job de retransmissão.</summary>
    public TimeSpan IntervaloRetransmissao { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// BACKOFF — idade mínima de um documento <c>NumeroAlocado</c> antes de ser candidato a
    /// retentativa (dá tempo da rede/SEFAZ se estabilizar antes de martelar de novo).
    /// </summary>
    public TimeSpan AntiguidadeMinimaParaRetentar { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// LIMITE DE TENTATIVAS — idade máxima antes de desistir formalmente do número
    /// (<c>DesistirDeNumeroUseCase</c>, protocolo de Inutilização de Numeração) em vez de retentar
    /// pra sempre. 24h é o prazo típico de janela operacional de um dia de loja; ajustável por
    /// config quando o prazo legal de inutilização da UF exigir outro valor.
    /// </summary>
    public TimeSpan IdadeMaximaAntesDeDesistir { get; set; } = TimeSpan.FromHours(24);
}
