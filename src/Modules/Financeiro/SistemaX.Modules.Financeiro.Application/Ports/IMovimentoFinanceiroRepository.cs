using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port do repositório de <see cref="MovimentoFinanceiro"/> — a fonte de verdade do caixa REALIZADO.</summary>
public interface IMovimentoFinanceiroRepository
{
    Task<MovimentoFinanceiro?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<MovimentoFinanceiro?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default);

    /// <summary>Já existe estorno registrado para este movimento? (idempotência de estorno).</summary>
    Task<MovimentoFinanceiro?> BuscarEstornoDeAsync(string movimentoOriginalId, CancellationToken ct = default);

    /// <summary>Movimentos ligados a uma <c>ContaAPagar</c>/<c>ContaAReceber</c> específica
    /// (<see cref="MovimentoFinanceiro.ContaOrigemId"/>) — usado por handlers de estorno de fato de
    /// origem (ex.: <c>compra.estornada</c>) quando o pagamento veio de uma baixa manual, cuja
    /// <c>SourceRef</c> é opaca (<c>BaixarParcelaComando.IdempotencyKey</c> do chamador) e não pode
    /// ser reconstruída a partir do id do fato de origem.</summary>
    Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorContaOrigemAsync(string businessId, string contaOrigemId, CancellationToken ct = default);

    Task SalvarAsync(MovimentoFinanceiro movimento, CancellationToken ct = default);

    Task<IReadOnlyList<MovimentoFinanceiro>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);

    /// <summary>Saldo em caixa é sempre DERIVADO (soma de movimentos) — nunca um campo armazenado (docs/financeiro-datamodel.md §2.2).</summary>
    Task<Money> CalcularSaldoAsync(string businessId, string? contaBancariaCaixaId, DateTimeOffset ateData, CancellationToken ct = default);
}
