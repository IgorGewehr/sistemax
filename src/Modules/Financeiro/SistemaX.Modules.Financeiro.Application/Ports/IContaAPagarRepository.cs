using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port do repositório de <see cref="ContaAPagar"/> — espelha <see cref="IContaAReceberRepository"/>.</summary>
public interface IContaAPagarRepository
{
    Task<ContaAPagar?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<ContaAPagar?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default);

    Task SalvarAsync(ContaAPagar conta, CancellationToken ct = default);

    Task<IReadOnlyList<ContaAPagar>> ListarPorCompetenciaAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);

    Task<IReadOnlyList<ContaAPagar>> ListarAbertasAteAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default);
}
