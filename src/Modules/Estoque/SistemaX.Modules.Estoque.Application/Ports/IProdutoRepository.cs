using SistemaX.Modules.Estoque.Domain.Catalogo;

namespace SistemaX.Modules.Estoque.Application.Ports;

/// <summary>Port do catálogo. Repositório não tem noção de saldo — isso vive em <see cref="ISaldoRepository"/>.</summary>
public interface IProdutoRepository
{
    Task<Produto?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<Produto?> ObterPorSkuAsync(string tenantId, string sku, CancellationToken ct = default);

    Task SalvarAsync(Produto produto, CancellationToken ct = default);

    Task<IReadOnlyList<Produto>> ListarAsync(string tenantId, CancellationToken ct = default);
}
