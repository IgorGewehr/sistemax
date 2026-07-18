using SistemaX.Modules.Financeiro.Domain.Ativos;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="AtivoDeCapital"/> — espelha <c>IProjetoRepository</c>.</summary>
public interface IAtivoDeCapitalRepository
{
    Task<AtivoDeCapital?> ObterPorIdAsync(string businessId, string ativoId, CancellationToken ct = default);

    Task<IReadOnlyList<AtivoDeCapital>> ListarAsync(string businessId, string? projetoId = null, CancellationToken ct = default);

    /// <summary>Só os ativos que ainda reconhecem competência — atalho para o cron
    /// (<c>ReconhecerAmortizacoesUseCase</c>).</summary>
    Task<IReadOnlyList<AtivoDeCapital>> ListarEmUsoAsync(string businessId, CancellationToken ct = default);

    Task SalvarAsync(AtivoDeCapital ativo, CancellationToken ct = default);
}
