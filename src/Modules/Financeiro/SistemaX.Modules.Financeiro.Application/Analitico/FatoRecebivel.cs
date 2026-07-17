namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// UMA linha de recebível — fold APPEND-ONLY de vendas/pedidos a receber
/// (docs/financeiro-datamodel.md §2.2), foldada do ledger por
/// <see cref="FatoRecebiveisProjection"/>. <see cref="Vencimento"/> é o dia (bucket no fuso do
/// tenant, ver <c>BucketingTemporalDoTenant</c>) em que o recebível nasce; <see cref="ValorLiquidoCentavos"/>
/// e <see cref="DataLiquidacaoPrevista"/> refletem o dinheiro DE VERDADE — já descontado o MDR da
/// forma de pagamento e deslocado pelo lag de liquidação D+N (ex.: crédito D+30 líquido de ~3,49%;
/// PIX/dinheiro D+0 sem taxa) — nunca o valor bruto/data de emissão puros.
///
/// SINAL: uma reversão (<c>venda.estornada</c>) lança uma linha NEGATIVA no dia do estorno,
/// compensando a original — nunca edita/apaga a linha original (mesma filosofia append-only do
/// restante das fact tables da F0/F1: <c>fato_receita_diaria</c> também nunca reabre o dia
/// original).
/// </summary>
public sealed record FatoRecebivel(
    string TenantId,
    string OrigemChave,
    DateOnly Vencimento,
    DateOnly DataLiquidacaoPrevista,
    string? FormaPagamento,
    decimal TaxaPercentualAplicada,
    long ValorBrutoCentavos,
    long ValorLiquidoCentavos,
    DateTimeOffset AtualizadoEmUtc);
