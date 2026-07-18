using SistemaX.Modules.Compras.Domain.Notas;

namespace SistemaX.Modules.Compras.Application.Ports;

public interface INotaDeCompraRepository
{
    Task<NotaDeCompra?> ObterPorIdAsync(string id, CancellationToken ct = default);

    /// <summary>Base do dedupe estrutural (plano §3.3 invariante 6) — reimportar o mesmo XML
    /// encontra a nota já existente em vez de duplicar.</summary>
    Task<NotaDeCompra?> ObterPorChaveDeAcessoAsync(string tenantId, string chaveDeAcesso, CancellationToken ct = default);

    Task SalvarAsync(NotaDeCompra nota, CancellationToken ct = default);

    /// <summary>Read-model da tela de Notas de Compra (achado de auditoria: até aqui só era
    /// possível resolver uma nota já sabendo o id ou a chave de acesso, nunca listar). Mais
    /// recente primeiro por data de emissão.</summary>
    Task<IReadOnlyList<NotaDeCompra>> ListarAsync(string tenantId, CancellationToken ct = default);
}
