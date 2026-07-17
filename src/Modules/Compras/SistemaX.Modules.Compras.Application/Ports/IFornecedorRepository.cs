using SistemaX.Modules.Compras.Domain.Fornecedores;

namespace SistemaX.Modules.Compras.Application.Ports;

public interface IFornecedorRepository
{
    Task<Fornecedor?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Dedupe por documento — só deve ser chamado com <paramref name="documento"/>
    /// não-vazio (ver nota de <c>Fornecedor</c> sobre a fusão indevida por documento vazio).</summary>
    Task<Fornecedor?> ObterPorDocumentoAsync(string tenantId, string documento, CancellationToken ct = default);

    Task SalvarAsync(Fornecedor fornecedor, CancellationToken ct = default);
}
