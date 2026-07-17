namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Chave de acesso (44 dígitos) da NF-e ORIGINAL sendo devolvida — gap #5 de
/// emissao-mapping.md §4.6/§11: <see cref="Domain.Documentos.DocumentoFiscal"/> não carrega essa
/// referência por desenho (é fato de Vendas/CRM sobre QUAL venda originou a devolução, não
/// tributário). Sem ela, <c>TipoOperacaoFiscal.DevolucaoDeVenda</c> resolve tributação e CFOP
/// corretamente mas o payload de devolução (<c>finalidade=4</c>) nunca fica completo — a SEFAZ
/// rejeita com 235/236 (NFref ausente). Mesma convenção de
/// <see cref="IDestinatarioDocumentoFiscalRepository"/>/<see cref="IFormaPagamentoDocumentoFiscalRepository"/>:
/// populado pelo caller (Vendas/PDV) antes da transmissão, nunca inferido pelo adapter.</summary>
public interface IReferenciaDevolucaoDocumentoFiscalRepository
{
    /// <summary>Chaveado pelo Id do <see cref="Domain.Documentos.DocumentoFiscal"/> DE
    /// DEVOLUÇÃO (não da nota original). <c>null</c> quando nenhuma referência foi vinculada
    /// (documento não é uma devolução, ou é uma devolução sem chave de acesso original
    /// disponível).</summary>
    Task<string?> ObterRefNFeAsync(string documentoFiscalId, CancellationToken ct = default);

    Task VincularAsync(string documentoFiscalId, string refNFe, CancellationToken ct = default);
}
