using SistemaX.Modules.Compras.Domain.Notas;

namespace SistemaX.Modules.Compras.Application.Ports;

public interface INotaDeCompraRepository
{
    Task<NotaDeCompra?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Base do dedupe estrutural (plano §3.3 invariante 6) — reimportar o mesmo XML
    /// encontra a nota já existente em vez de duplicar.</summary>
    Task<NotaDeCompra?> ObterPorChaveDeAcessoAsync(string tenantId, string chaveDeAcesso, CancellationToken ct = default);

    Task SalvarAsync(NotaDeCompra nota, CancellationToken ct = default);
}
