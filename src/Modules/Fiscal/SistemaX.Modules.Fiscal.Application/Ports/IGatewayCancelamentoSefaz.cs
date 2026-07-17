using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Resultado de um pedido de cancelamento — evento SEFAZ distinto de emissão (protocolo
/// próprio, nunca reaproveita <see cref="ResultadoTransmissaoSefaz.Protocolo"/> da autorização
/// original).</summary>
public sealed record ResultadoCancelamentoSefaz(string Protocolo, DateTimeOffset CanceladoEm);

/// <summary>
/// Porta irmã de <see cref="IGatewayEmissaoSefaz"/> — cancelamento é operação SEFAZ distinta de
/// emissão (mapeia para <c>/nfe/cancelar</c>), extensão aditiva prevista desde a declaração
/// original do port de emissão (emissao-mapping.md §9). Chamada DEPOIS que
/// <see cref="DocumentoFiscal.Cancelar"/> já validou localmente (justificativa &gt;= 15
/// caracteres) — o adapter nunca reimplementa essa regra, só transmite o evento já validado.
/// </summary>
public interface IGatewayCancelamentoSefaz
{
    Task<Result<ResultadoCancelamentoSefaz>> CancelarAsync(DocumentoFiscal documento, string justificativa, CancellationToken ct = default);
}
