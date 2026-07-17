using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Porta irmã de <see cref="IGatewayEmissaoSefaz"/> — inutilização de faixa de numeração é
/// operação SEFAZ distinta de emissão (mapeia para <c>/nfe/inutilizar</c>), extensão aditiva
/// prevista desde a declaração original do port de emissão (emissao-mapping.md §9). Alimentada
/// por um job periódico que agrega <c>NumeroFiscalInutilizadoDomainEvent</c> pendentes
/// (docs/fiscal/arquitetura.md §5) — <c>numeroInicial == numeroFinal</c> quando é 1 documento só;
/// o schema aceita fechar um INTERVALO para protocolar 1 inutilização de faixa em vez de N
/// individuais.
/// </summary>
public interface IGatewayInutilizacaoSefaz
{
    Task<Result> InutilizarAsync(
        string tenantId, string cnpj, string modelo, string serie, long numeroInicial, long numeroFinal,
        string justificativa, string ufEmitente, CancellationToken ct = default);
}
