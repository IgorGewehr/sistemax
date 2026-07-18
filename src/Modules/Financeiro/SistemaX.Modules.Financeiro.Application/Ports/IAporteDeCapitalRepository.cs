using SistemaX.Modules.Financeiro.Domain.Ativos;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="AporteDeCapital"/> — espelha <c>IAtivoDeCapitalRepository</c>,
/// mas com <c>ExcluirAsync</c> físico (Decisão DI5 do design de Imobilizado/ROI — registro
/// gerencial sem lançamento contábil, sem FSM, deletável).</summary>
public interface IAporteDeCapitalRepository
{
    Task<AporteDeCapital?> ObterPorIdAsync(string businessId, string aporteId, CancellationToken ct = default);

    Task<IReadOnlyList<AporteDeCapital>> ListarAsync(string businessId, CancellationToken ct = default);

    Task SalvarAsync(AporteDeCapital aporte, CancellationToken ct = default);

    /// <summary>Delete físico (§3.3/DI5 do design) — retorna <c>true</c> se algo foi de fato
    /// removido (idempotência do lado do chamador: excluir de novo não é erro, só devolve <c>false</c>).</summary>
    Task<bool> ExcluirAsync(string businessId, string aporteId, CancellationToken ct = default);
}
