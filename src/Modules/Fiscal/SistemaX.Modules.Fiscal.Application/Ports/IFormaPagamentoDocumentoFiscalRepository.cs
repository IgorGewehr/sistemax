using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Forma de pagamento de UM documento — gap #3 de emissao-mapping.md §4.5/§11: fato de
/// Venda/PDV, não de tributação, propositalmente fora do agregado <c>DocumentoFiscal</c>. Nunca
/// persistido no agregado fiscal — só repassado como dado auxiliar de transmissão (o
/// <c>SourceRef</c> do documento já aponta pra onde essa informação vive de verdade, em
/// Vendas).</summary>
public sealed record FormaPagamentoParaEmitir(string Metodo, Money Valor);

public interface IFormaPagamentoDocumentoFiscalRepository
{
    /// <summary>Chaveado pelo Id do <see cref="Domain.Documentos.DocumentoFiscal"/>. Lista vazia
    /// (nunca null) quando nenhuma forma foi vinculada ainda.</summary>
    Task<IReadOnlyList<FormaPagamentoParaEmitir>> ObterPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default);

    Task VincularAsync(string documentoFiscalId, IReadOnlyList<FormaPagamentoParaEmitir> pagamentos, CancellationToken ct = default);
}
