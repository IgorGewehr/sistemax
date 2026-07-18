using SistemaX.Modules.Fiscal.Domain.Documentos;

namespace SistemaX.Modules.Fiscal.Application.Ports;

/// <summary>Persistência do log de CC-e (side-channel, ver <see cref="CartaCorrecaoFiscal"/>) —
/// nunca reidratado num <c>DocumentoFiscal</c>, só consultado por
/// <c>EmitirCartaCorrecaoUseCase</c> (calcular o próximo <c>Sequencia</c>) e pela tela de detalhe
/// do documento (histórico de correções).</summary>
public interface ICartaCorrecaoFiscalRepository
{
    Task<IReadOnlyList<CartaCorrecaoFiscal>> ListarPorDocumentoAsync(string documentoFiscalId, CancellationToken ct = default);

    Task SalvarAsync(CartaCorrecaoFiscal carta, CancellationToken ct = default);
}
