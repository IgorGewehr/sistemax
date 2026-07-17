using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>
/// Port do repositório de <see cref="ContaAReceber"/>. <see cref="BuscarPorOrigemAsync"/> é a
/// PEÇA-CHAVE de idempotência (R3/docs/financeiro-datamodel.md §4.3): todo handler de evento de
/// integração consulta por aqui, usando <c>SourceRef.Chave</c>, antes de criar uma conta nova.
/// </summary>
public interface IContaAReceberRepository
{
    Task<ContaAReceber?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<ContaAReceber?> BuscarPorOrigemAsync(string businessId, string sourceRefChave, CancellationToken ct = default);

    Task SalvarAsync(ContaAReceber conta, CancellationToken ct = default);

    Task<IReadOnlyList<ContaAReceber>> ListarPorCompetenciaAsync(string businessId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);

    /// <summary>Contas com parcela(s) em aberto/parcial vencendo até <paramref name="referencia"/> — alimenta fluxo de caixa projetado e a régua de vencimento.</summary>
    Task<IReadOnlyList<ContaAReceber>> ListarAbertasAteAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default);
}
