using SistemaX.Modules.Compras.Domain.Vinculos;

namespace SistemaX.Modules.Compras.Application.Ports;

/// <summary>Port do de-para aprendido (plano §5/§6) — a Infrastructure garante unicidade por
/// <c>(TenantId, FornecedorId, CodigoProdutoNoFornecedor)</c> na chave de armazenamento.</summary>
public interface IVinculoProdutoFornecedorRepository
{
    Task<VinculoProdutoFornecedor?> ObterAsync(
        string tenantId, string fornecedorId, string codigoProdutoNoFornecedor, CancellationToken ct = default);

    Task SalvarAsync(VinculoProdutoFornecedor vinculo, CancellationToken ct = default);
}
