using SistemaX.Modules.Compras.Domain.Fornecedores;

namespace SistemaX.Modules.Compras.Application.Ports;

public interface IFornecedorRepository
{
    Task<Fornecedor?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Dedupe por documento — só deve ser chamado com <paramref name="documento"/>
    /// não-vazio (ver nota de <c>Fornecedor</c> sobre a fusão indevida por documento vazio).</summary>
    Task<Fornecedor?> ObterPorDocumentoAsync(string tenantId, string documento, CancellationToken ct = default);

    Task SalvarAsync(Fornecedor fornecedor, CancellationToken ct = default);

    /// <summary>Read-model da tela de Fornecedores (achado de auditoria: até aqui só era possível
    /// resolver um fornecedor já sabendo o id ou o documento, nunca listar). Nome ascendente —
    /// convenção de cadastro (diferente das listagens de fato transacional, que ordenam por data
    /// desc).</summary>
    Task<IReadOnlyList<Fornecedor>> ListarAsync(string tenantId, CancellationToken ct = default);
}
