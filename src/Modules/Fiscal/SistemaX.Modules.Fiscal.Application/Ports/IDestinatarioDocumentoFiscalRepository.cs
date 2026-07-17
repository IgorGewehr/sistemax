namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Endereço do destinatário/consumidor/tomador — usado só quando o documento carrega
/// endereço (ex.: NF-e com finalidade de devolução, ou destinatário contribuinte
/// interestadual).</summary>
public sealed record EnderecoDestinatarioFiscal(
    string Logradouro, string Numero, string? Complemento, string Bairro,
    string CodigoMunicipio, string Municipio, string Uf, string Cep);

/// <summary>Cliente da nota (NF-e <c>destinatario</c> / NFC-e <c>consumidor</c> / NFS-e
/// <c>tomador</c>) — gap #1 de emissao-mapping.md §4.2/§11: <see cref="DocumentoFiscal"/> não
/// carrega nenhum dado de cliente por desenho (é fato de CRM/Vendas, não de tributação).
/// Nullable por natureza — NFC-e frequentemente não identifica o consumidor.</summary>
public sealed record DestinatarioDocumentoFiscal(
    string? Cnpj,
    string? Cpf,
    string Nome,
    string? Email,
    string? InscricaoEstadual,
    EnderecoDestinatarioFiscal? Endereco);

public interface IDestinatarioDocumentoFiscalRepository
{
    /// <summary>Chaveado pelo Id do <see cref="Domain.Documentos.DocumentoFiscal"/> — populado
    /// pelo caller (Vendas/PDV) antes da transmissão, nunca inferido pelo adapter.</summary>
    Task<DestinatarioDocumentoFiscal?> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default);

    Task VincularAsync(string documentoFiscalId, DestinatarioDocumentoFiscal destinatario, CancellationToken ct = default);
}
