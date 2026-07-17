using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>
/// Resultado de uma tentativa de transmissão à SEFAZ. Exatamente um dos três desfechos possíveis
/// da FSM a partir de <see cref="StatusDocumentoFiscal.NumeroAlocado"/>
/// (docs/fiscal/arquitetura.md §2.6/§7): <see cref="StatusDocumentoFiscal.Autorizado"/> carrega
/// <see cref="ChaveDeAcesso"/> + <see cref="AutorizadoEm"/>; <see cref="StatusDocumentoFiscal.Rejeitado"/>/
/// <see cref="StatusDocumentoFiscal.Denegado"/> carregam <see cref="Motivo"/>. Quem consome este
/// resultado (<c>CasosDeUso.TransmitirDocumentoFiscalUseCase</c>) só sabe repassar para
/// <see cref="DocumentoFiscal.RegistrarAutorizacao"/>/<see cref="DocumentoFiscal.RegistrarRejeicao"/>/
/// <see cref="DocumentoFiscal.RegistrarDenegacao"/> — nunca decide o desfecho por conta própria.
/// </summary>
public sealed record ResultadoTransmissaoSefaz
{
    public required StatusDocumentoFiscal Status { get; init; }
    public string? ChaveDeAcesso { get; init; }
    public string? Protocolo { get; init; }
    public DateTimeOffset? AutorizadoEm { get; init; }
    public string? Motivo { get; init; }

    /// <summary>Quando true, a SEFAZ recebeu mas ainda não decidiu (gap #9 — emissao-mapping.md
    /// §7.2) — o caller NÃO deve chamar nenhum Registrar*() do agregado (ele continua
    /// NumeroAlocado); deve agendar uma CONSULTA de status mais tarde, nunca uma retransmissão.</summary>
    public bool AindaProcessando { get; init; }

    public static ResultadoTransmissaoSefaz Autorizado(string chaveDeAcesso, string? protocolo, DateTimeOffset autorizadoEm) =>
        new() { Status = StatusDocumentoFiscal.Autorizado, ChaveDeAcesso = chaveDeAcesso, Protocolo = protocolo, AutorizadoEm = autorizadoEm };

    public static ResultadoTransmissaoSefaz Rejeitado(string motivo) =>
        new() { Status = StatusDocumentoFiscal.Rejeitado, Motivo = motivo };

    public static ResultadoTransmissaoSefaz Denegado(string motivo) =>
        new() { Status = StatusDocumentoFiscal.Denegado, Motivo = motivo };

    /// <summary>SEFAZ ainda não decidiu (§7.2) — nenhuma transição de FSM deveria ser tentada com
    /// este resultado; o <see cref="Status"/> aqui é só um placeholder (NumeroAlocado, "nada
    /// mudou do ponto de vista do agregado").</summary>
    public static ResultadoTransmissaoSefaz Processando() =>
        new() { Status = StatusDocumentoFiscal.NumeroAlocado, AindaProcessando = true };
}

/// <summary>
/// Porta de assinatura/transmissão SEFAZ — o pedaço de <c>DocumentoFiscal.NumeroAlocado →
/// Autorizado/Rejeitado/Denegado</c> que <see cref="Application.CasosDeUso.EmitirDocumentoFiscalUseCase"/>
/// deliberadamente NÃO chama (ver comentário na classe): esta fase só resolve tributação e aloca
/// número, a transmissão em si fica para quem implementar este port.
///
/// DECISÃO DE PRODUTO PENDENTE (não arquitetura) — a virar ADR separado antes de qualquer
/// <c>Infrastructure</c> concreta implementar este port (docs/fiscal/arquitetura.md §9):
/// gateway terceiro pago (ex.: um provedor de emissão gerenciada, mais rápido de integrar, custo
/// recorrente por documento) vs. emissão própria via mTLS + XMLDSig direto com a SEFAZ (sem custo
/// por documento, mais superfície própria de manter — certificado A1/A3, schemas XSD por UF,
/// contingência SVC-AN/SVC-RS). Nenhuma das duas opções muda esta assinatura: ambas recebem um
/// <see cref="DocumentoFiscal"/> já em <see cref="StatusDocumentoFiscal.NumeroAlocado"/> (itens
/// resolvidos, número comprometido) e devolvem um <see cref="ResultadoTransmissaoSefaz"/> — a
/// escolha de COMO montar/assinar/enviar o XML fica inteiramente dentro do adapter.
///
/// Escopo desta porta é só a EMISSÃO (o caminho que fecha a lacuna descrita em §9). Protocolo de
/// CANCELAMENTO (janela de 30min pós-autorização, `DocumentoFiscal.Cancelar` já existe no domínio)
/// e de INUTILIZAÇÃO de numeração (`DesistirDeNumeroUseCase` já existe) são operações SEFAZ
/// distintas — extensão aditiva deste port (ou ports irmãos) quando o gateway concreto for
/// implementado, não escopo desta declaração de contrato.
/// </summary>
public interface IGatewayEmissaoSefaz
{
    /// <summary>Assina e transmite um <see cref="DocumentoFiscal"/> em
    /// <see cref="StatusDocumentoFiscal.NumeroAlocado"/> (ou <see cref="StatusDocumentoFiscal.Rejeitado"/>,
    /// retransmissão). Despacha internamente para <c>/nfe/emitir</c>, <c>/nfe/nfce/emitir</c> ou
    /// <c>/nfse/emitir</c> conforme <see cref="DocumentoFiscal.Tipo"/> — a assinatura não muda por
    /// tipo de documento (emissao-mapping.md §2). <see cref="Result.Falhar"/> aqui é reservado a
    /// falha de INFRAESTRUTURA (timeout, indisponibilidade da SEFAZ, auth) — nunca para o desfecho
    /// de negócio "rejeitado"/"denegado", que é sempre <see cref="Result.Ok"/> com o
    /// <see cref="ResultadoTransmissaoSefaz"/> correspondente (a SEFAZ respondeu, só não
    /// autorizou).</summary>
    Task<Result<ResultadoTransmissaoSefaz>> TransmitirAsync(DocumentoFiscal documento, CancellationToken ct = default);

    /// <summary>Mapeia para <c>/nfe/consultar</c> — usado quando <see cref="ResultadoTransmissaoSefaz.AindaProcessando"/>
    /// foi true numa transmissão anterior, por um job periódico de consulta (gap #9,
    /// emissao-mapping.md §7.2), nunca por retransmissão do XML.</summary>
    Task<Result<ResultadoTransmissaoSefaz>> ConsultarAsync(DocumentoFiscal documento, CancellationToken ct = default);
}
