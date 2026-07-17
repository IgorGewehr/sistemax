using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Application.Ports;

public interface IDocumentoFiscalRepository
{
    Task<DocumentoFiscal?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Chave de idempotência — <c>UNIQUE(tenant_id, origem_modulo, origem_id)</c> no
    /// SQLite (docs/fiscal/arquitetura.md §8). Reprocessar o mesmo evento de integração encontra
    /// o documento já existente e vira NO-OP no handler.</summary>
    Task<DocumentoFiscal?> ObterPorOrigemAsync(string tenantId, string origemChave, CancellationToken ct = default);

    Task SalvarAsync(DocumentoFiscal documento, CancellationToken ct = default);

    /// <summary>Documentos com número alocado há mais de <paramref name="antesDe"/> que nunca
    /// chegaram a um status terminal — insumo do job periódico que roteia para
    /// <c>DesistirDeNumeroUseCase</c> (docs/fiscal/arquitetura.md §5).</summary>
    Task<IReadOnlyList<DocumentoFiscal>> ListarNumeroAlocadoAntesDeAsync(string tenantId, DateTimeOffset antesDe, CancellationToken ct = default);
}
