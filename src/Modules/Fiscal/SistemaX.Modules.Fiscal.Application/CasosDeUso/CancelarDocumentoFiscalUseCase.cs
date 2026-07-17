using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>Cancela um documento AUTORIZADO — FSM garante que só terminais válidos aceitam a
/// transição (docs/fiscal/arquitetura.md §2.6). Nunca dispara reversão financeira sozinho.</summary>
public sealed class CancelarDocumentoFiscalUseCase(IDocumentoFiscalRepository documentos, IIntegrationEventBus bus)
{
    public async Task<Result<DocumentoFiscal>> ExecutarAsync(string documentoFiscalId, string justificativa, CancellationToken ct = default)
    {
        var documento = await documentos.ObterPorIdAsync(documentoFiscalId, ct);
        if (documento is null)
            return Result.Falhar<DocumentoFiscal>(new Error("fiscal.documento.nao_encontrado", $"Documento fiscal '{documentoFiscalId}' não encontrado."));

        var resultado = documento.Cancelar(justificativa);
        if (resultado.Falha)
            return Result.Falhar<DocumentoFiscal>(resultado.Erro);

        await documentos.SalvarAsync(documento, ct);

        foreach (var evento in documento.DomainEvents.OfType<DocumentoFiscalCanceladoDomainEvent>())
            await bus.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        documento.ClearDomainEvents();
        return Result.Ok(documento);
    }
}
