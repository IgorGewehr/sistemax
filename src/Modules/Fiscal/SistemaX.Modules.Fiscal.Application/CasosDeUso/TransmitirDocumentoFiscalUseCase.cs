using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>
/// Chama <see cref="IGatewayEmissaoSefaz"/> por um <see cref="DocumentoFiscal"/> já em
/// <see cref="StatusDocumentoFiscal.NumeroAlocado"/> e registra o desfecho na FSM — o pedaço que
/// <see cref="EmitirDocumentoFiscalUseCase"/> chama na primeira tentativa (logo após
/// <c>AlocarNumero</c>) e que <see cref="RetransmitirDocumentosPendentesUseCase"/> (job de retry)
/// chama de novo para documentos que ficaram presos em <c>NumeroAlocado</c> por falha de
/// infraestrutura (gap #9/§7.2 de docs/fiscal/emissao-mapping.md — o "job de retransmissão" que os
/// comentários da versão anterior desta classe citavam como necessário e ainda não existia).
///
/// Falha de INFRAESTRUTURA do gateway (timeout, SEFAZ indisponível) nunca falha este caso de uso —
/// o documento permanece <c>NumeroAlocado</c>, retentável de novo depois; só rejeição/denegação/
/// autorização (a SEFAZ respondeu) avança a FSM.
/// </summary>
public sealed class TransmitirDocumentoFiscalUseCase(
    IDocumentoFiscalRepository documentos,
    IIntegrationEventBus bus,
    IGatewayEmissaoSefaz gateway)
{
    /// <summary>Primeira tentativa (ou retransmissão) — assina/envia o XML de novo.</summary>
    public Task<Result<DocumentoFiscal>> ExecutarAsync(DocumentoFiscal documento, CancellationToken ct = default)
        => RegistrarDesfechoAsync(documento, () => gateway.TransmitirAsync(documento, ct), ct);

    /// <summary>Consulta o status de uma transmissão anterior que devolveu
    /// <see cref="ResultadoTransmissaoSefaz.AindaProcessando"/> — mapeia para
    /// <c>/nfe/consultar</c>, nunca reenvia o XML.</summary>
    public Task<Result<DocumentoFiscal>> ConsultarAsync(DocumentoFiscal documento, CancellationToken ct = default)
        => RegistrarDesfechoAsync(documento, () => gateway.ConsultarAsync(documento, ct), ct);

    private async Task<Result<DocumentoFiscal>> RegistrarDesfechoAsync(
        DocumentoFiscal documento, Func<Task<Result<ResultadoTransmissaoSefaz>>> chamarGateway, CancellationToken ct)
    {
        var transmissao = await chamarGateway();
        if (transmissao.Falha)
            return Result.Ok(documento); // infra — documento permanece NumeroAlocado, retentável depois

        var resultado = transmissao.Valor;
        if (resultado.AindaProcessando)
            return Result.Ok(documento); // SEFAZ ainda não decidiu — nada muda, próxima rodada do job assume

        var registro = resultado.Status switch
        {
            StatusDocumentoFiscal.Autorizado => documento.RegistrarAutorizacao(resultado.ChaveDeAcesso!, resultado.Protocolo, resultado.AutorizadoEm!.Value),
            StatusDocumentoFiscal.Rejeitado => documento.RegistrarRejeicao(resultado.Motivo ?? "Rejeitado pela SEFAZ (motivo não informado)."),
            StatusDocumentoFiscal.Denegado => documento.RegistrarDenegacao(resultado.Motivo ?? "Denegado pela SEFAZ (motivo não informado)."),
            _ => Result.Falhar(new Error("fiscal.emissao.status_inesperado", $"Gateway devolveu status '{resultado.Status}' fora do esperado.")),
        };
        if (registro.Falha) return Result.Falhar<DocumentoFiscal>(registro.Erro);

        await documentos.SalvarAsync(documento, ct);

        foreach (var evento in documento.DomainEvents.OfType<DocumentoFiscalAutorizadoDomainEvent>())
            await bus.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        documento.ClearDomainEvents();
        return Result.Ok(documento);
    }
}
