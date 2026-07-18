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

    /// <summary>Imobilizado/Painel de ROI (docs/financeiro/design-imobilizado-roi.md §7.0/§12 I3) —
    /// TODAS as contas do tenant com a <c>categoriaId</c> informada, independente de status ou
    /// competência: <c>RoiDoNegocioService</c> precisa das PARCELAS (pagas E em aberto) de
    /// <c>ativo-de-capital</c> para montar <c>Capex_m</c> (caixa) e a projeção de parcelas restantes
    /// — as duas outras consultas (por competência/por vencimento em aberto) não servem porque uma
    /// filtra por competência (não por pagamento) e a outra omite contas já 100% liquidadas.</summary>
    Task<IReadOnlyList<ContaAPagar>> ListarPorCategoriaAsync(string businessId, string categoriaId, CancellationToken ct = default);
}
