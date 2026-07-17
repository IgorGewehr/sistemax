using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface ITributacaoProdutoRepository
{
    Task<TributacaoProduto?> ObterAsync(string tenantId, string produtoId, CancellationToken ct = default);

    Task SalvarAsync(TributacaoProduto tributacao, CancellationToken ct = default);
}
