using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Application.CasosDeUso;

/// <summary>Fecha formalmente um número que foi alocado mas nunca chegou a autorizar (rascunho
/// abandonado, venda cancelada antes da transmissão, crash seguido de nova tentativa) — nunca
/// deixa o documento "pairando" nem reaproveita o número para outro documento
/// (docs/fiscal/arquitetura.md §5). O evento resultante alimenta o job periódico que protocola a
/// Inutilização de Numeração na SEFAZ dentro do prazo legal.</summary>
public sealed class DesistirDeNumeroUseCase(IDocumentoFiscalRepository documentos, IIntegrationEventBus bus)
{
    public async Task<Result<DocumentoFiscal>> ExecutarAsync(string documentoFiscalId, string motivo, CancellationToken ct = default)
    {
        var documento = await documentos.ObterPorIdAsync(documentoFiscalId, ct);
        if (documento is null)
            return Result.Falhar<DocumentoFiscal>(new Error("fiscal.documento.nao_encontrado", $"Documento fiscal '{documentoFiscalId}' não encontrado."));

        var resultado = documento.Desistir(motivo);
        if (resultado.Falha)
            return Result.Falhar<DocumentoFiscal>(resultado.Erro);

        await documentos.SalvarAsync(documento, ct);

        foreach (var evento in documento.DomainEvents.OfType<NumeroFiscalInutilizadoDomainEvent>())
            await bus.PublishAsync(evento.ParaEventoDeIntegracao(), ct);

        documento.ClearDomainEvents();
        return Result.Ok(documento);
    }
}
