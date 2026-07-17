using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>
/// Persistência de <see cref="FormaDePagamento"/> — o LAR ÚNICO de MDR/lag por forma de pagamento.
/// <see cref="ObterPorNomeAsync"/> é o método que <c>FatoRecebiveisProjection</c> usa para resolver
/// taxa/lag a partir do rótulo livre que chega em <c>VendaConcluida.FormaPagamento</c>/
/// <c>PedidoPago.FormaPagamento</c> — case-insensitive, mesma semântica de
/// <c>ClassificadorFormaPagamento</c>/da antiga <c>ConfiguracaoDeRecebiveisOptions.Resolver</c>
/// (removida nesta reconciliação: uma forma não encontrada não inventa desconto).
/// </summary>
public interface IFormaDePagamentoRepository
{
    Task<FormaDePagamento?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default);

    Task<FormaDePagamento?> ObterPorNomeAsync(string businessId, string nome, CancellationToken ct = default);

    Task<IReadOnlyList<FormaDePagamento>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default);

    Task SalvarAsync(FormaDePagamento forma, CancellationToken ct = default);
}
