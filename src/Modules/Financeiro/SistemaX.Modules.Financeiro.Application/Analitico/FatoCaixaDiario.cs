namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fact table de PROVA da F0 do plano de inteligência do Financeiro
/// (docs/financeiro/inteligencia-arquitetura.md/ADR-0005) — caixa diário REALIZADO,
/// foldado do ledger <c>integration_events</c> por <see cref="FatoCaixaDiarioProjection"/>.
/// <see cref="SaldoDiaCentavos"/> é sempre DERIVADO (entradas - saídas), nunca armazenado — mesmo
/// princípio de <c>IMovimentoFinanceiroRepository.CalcularSaldoAsync</c>.
///
/// LIMITAÇÃO CONHECIDA DA F0 (documentada, não escondida): só soma entradas à vista
/// (<c>VendaConcluida</c> classificada por <c>ClassificadorFormaPagamento.EhAVista</c> +
/// <c>PedidoPago</c>) e saídas de estorno (<c>VendaEstornada</c>). Caixa projetado com lag de
/// cartão/MDR (catálogo #1/#8) e saída por liquidação de <c>ContaAPagar</c> são Fase 1 — a F0 só
/// prova que o pipeline ledger→fold→fact table funciona ponta-a-ponta.
/// </summary>
public sealed record FatoCaixaDiario(string TenantId, DateOnly Dia, long EntradasCentavos, long SaidasCentavos, DateTimeOffset AtualizadoEmUtc)
{
    public long SaldoDiaCentavos => EntradasCentavos - SaidasCentavos;
}
