using SistemaX.Modules.Estoque.Domain.Razao;

namespace SistemaX.Modules.Estoque.Application.Ports;

/// <summary>
/// Port do RAZÃO — append-only por construção: não existe método de update/delete aqui, só
/// <see cref="SalvarAsync"/> (insert) e leituras. Idempotência é responsabilidade de quem chama:
/// consultar <see cref="ExisteComChaveAsync"/> ANTES de montar o movimento.
/// </summary>
public interface IMovimentoRepository
{
    Task<bool> ExisteComChaveAsync(string chaveIdempotencia, CancellationToken ct = default);

    Task SalvarAsync(MovimentoDeEstoque movimento, CancellationToken ct = default);

    /// <summary>Movimentos gerados por um fato de origem específico (ex.: todas as saídas de uma
    /// venda) — usado pelo estorno para espelhar exatamente o que saiu.</summary>
    Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorOrigemAsync(string tenantId, string sourceRefChave, CancellationToken ct = default);

    /// <summary>Razão completo de um produto×depósito, em ordem cronológica — a base do Kardex e
    /// do replay de <c>RecalcularSaldoUseCase</c>.</summary>
    Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorProdutoAsync(string tenantId, string produtoId, string depositoId, CancellationToken ct = default);

    Task<IReadOnlyList<MovimentoDeEstoque>> ListarPorPeriodoAsync(string tenantId, DateTimeOffset inicio, DateTimeOffset fim, CancellationToken ct = default);
}
