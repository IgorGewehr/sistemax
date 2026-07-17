using SistemaX.Modules.Financeiro.Domain.Assinaturas;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="Assinatura"/> (receita recorrente por cliente).</summary>
public interface IAssinaturaRepository
{
    Task<IReadOnlyList<Assinatura>> ListarAsync(string businessId, CancellationToken ct = default);

    /// <summary>Só as ativas — atalho para os geradores de cobrança e o cálculo de MRR.</summary>
    Task<IReadOnlyList<Assinatura>> ListarAtivasAsync(string businessId, CancellationToken ct = default);

    Task<Assinatura?> BuscarAsync(string businessId, string assinaturaId, CancellationToken ct = default);

    Task SalvarAsync(Assinatura assinatura, CancellationToken ct = default);
}
