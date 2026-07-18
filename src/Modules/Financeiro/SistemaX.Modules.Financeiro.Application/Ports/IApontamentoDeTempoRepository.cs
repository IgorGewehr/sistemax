using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="ApontamentoDeTempo"/> — espelha <c>IProjetoRepository</c>.
/// Delete FÍSICO permitido (design-pai §3.4): não há <c>Cancelar</c>/FSM.</summary>
public interface IApontamentoDeTempoRepository
{
    Task<ApontamentoDeTempo?> ObterPorIdAsync(string businessId, string apontamentoId, CancellationToken ct = default);

    Task<IReadOnlyList<ApontamentoDeTempo>> ListarAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, string? projetoId = null, string? clienteId = null, CancellationToken ct = default);

    Task SalvarAsync(ApontamentoDeTempo apontamento, CancellationToken ct = default);

    Task<bool> ExcluirAsync(string businessId, string apontamentoId, CancellationToken ct = default);
}
