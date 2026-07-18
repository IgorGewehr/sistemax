namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fact table de PROVA da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md/ADR-0005) — caixa diário REALIZADO,
/// foldado do ledger <c>integration_events</c> por <see cref="FatoCaixaDiarioProjection"/>.
/// <see cref="SaldoDiaCentavos"/> é sempre DERIVADO (entradas - saídas), nunca armazenado — mesmo
/// princípio de <c>IMovimentoFinanceiroRepository.CalcularSaldoAsync</c>.
///
/// BILATERAL (P1-3, docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): entradas à vista
/// (<c>VendaConcluida</c> classificada por <c>ClassificadorFormaPagamento.EhAVista</c> +
/// <c>PedidoPago</c>), a reversão de estorno (<c>VendaEstornada</c>) E toda liquidação de parcela —
/// SAÍDA de <c>ContaAPagar</c> (folha, compras, despesas, comissão) e ENTRADA a prazo de
/// <c>ContaAReceber</c> (ex.: cartão em D+N, já líquida de MDR — P1-6) via <c>ParcelaBaixada</c>.
/// Antes desta fatia, só o lado das entradas alimentava o fato — bandas de fluxo e burn EWMA
/// nunca viam queima de caixa real.
/// </summary>
public sealed record FatoCaixaDiario(string TenantId, DateOnly Dia, long EntradasCentavos, long SaidasCentavos, DateTimeOffset AtualizadoEmUtc)
{
    public long SaldoDiaCentavos => EntradasCentavos - SaidasCentavos;
}
