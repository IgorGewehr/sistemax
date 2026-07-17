using SistemaX.Modules.Financeiro.Domain.Contabil;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>
/// Port do motor invisível — o "trial balance" do negócio vive aqui. Nunca exposto na UI
/// operacional; só no "modo detalhado" opt-in (docs/financeiro-features.md §6).
/// </summary>
public interface ILancamentoContabilRepository
{
    Task<LancamentoContabil?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<LancamentoContabil?> BuscarPorOrigemAsync(string businessId, string origemChave, CancellationToken ct = default);

    Task SalvarAsync(LancamentoContabil lancamento, CancellationToken ct = default);

    Task<IReadOnlyList<LancamentoContabil>> ListarPorPeriodoAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);
}
