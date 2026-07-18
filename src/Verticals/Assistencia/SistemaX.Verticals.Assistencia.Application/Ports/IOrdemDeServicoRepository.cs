using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Application.Ports;

/// <summary>
/// Port do repositório de <see cref="OrdemDeServico"/> — mesmo formato de <c>IVendaRepository</c>
/// (Vendas.Application): busca/salva o agregado inteiro, a Infrastructure decide a granularidade
/// física. A OS não tem o mesmo requisito de crash-safety linha-a-linha do carrinho do PDV (ela só
/// muda por transição de FSM, não por digitação item-a-item), então salvar o agregado inteiro a
/// cada caso de uso é suficiente.
/// </summary>
public interface IOrdemDeServicoRepository
{
    Task<OrdemDeServico?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task SalvarAsync(OrdemDeServico ordemDeServico, CancellationToken ct = default);

    /// <summary>Read-model da fila de OS (achado de auditoria: até aqui só era possível resolver
    /// uma OS já sabendo o id, nunca listar/filtrar a fila). Mais recente primeiro
    /// (<c>AbertaEm</c> desc) — mesma convenção das demais listagens do sistema.
    /// <paramref name="status"/> nulo lista todos os status.</summary>
    Task<IReadOnlyList<OrdemDeServico>> ListarAsync(string tenantId, StatusOrdemServico? status = null, CancellationToken ct = default);
}
