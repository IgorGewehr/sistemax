namespace SistemaX.Modules.Financeiro.Domain.Assinaturas;

/// <summary>
/// Ciclo de vida de uma <see cref="Assinatura"/>.
///
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — ATUALIZAÇÃO da decisão original: este enum
/// dizia "inadimplência não é status aqui, é derivada dos recebíveis em atraso". Isso mudou por
/// necessidade de negócio: dunning exige um RELÓGIO de graça (<see cref="Assinatura.InadimplenteDesde"/>)
/// e uma transição explícita consumida de <c>ParcelaVencida</c>/<c>ParcelaBaixada</c>
/// (<c>DunningAssinaturaHandler</c>) — "derivar toda vez" não dá lugar pra guardar desde quando a
/// graça está correndo nem pra decidir, de forma auditável, quando ela virou churn. <see cref="Inadimplente"/>
/// é tratada como "ainda corrente" pelo MRR (<c>ReceitaRecorrenteService</c> conta
/// <see cref="Ativa"/> e <see cref="Inadimplente"/> igualmente) — só <see cref="Cancelada"/> (via
/// dunning expirado ou cancelamento direto) remove de fato.
/// </summary>
public enum StatusAssinatura
{
    Ativa,
    Pausada,
    Cancelada,
    Inadimplente
}
