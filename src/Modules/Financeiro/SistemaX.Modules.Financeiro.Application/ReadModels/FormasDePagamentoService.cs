using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma forma de pagamento com MDR/lag — o mesmo par que <c>FatoRecebiveisProjection</c>
/// consulta via <c>IFormaDePagamentoRepository.ObterPorNomeAsync</c>, aqui exposto para a tela
/// Bancário (painel "Ver por forma" do Super Consultor — docs/wiring/financeiro-telas-restantes.md §3).</summary>
public sealed record FormaDePagamentoResumo(
    string Id, string Nome, string Tipo, decimal MdrPercentual, int LagLiquidacaoDias, string? ContaLiquidacaoId, bool Ativo);

public sealed class FormasDePagamentoService(IFormaDePagamentoRepository formas)
{
    public async Task<IReadOnlyList<FormaDePagamentoResumo>> ListarAsync(string businessId, CancellationToken ct = default)
    {
        var todas = await formas.ListarAsync(businessId, apenasAtivas: false, ct).ConfigureAwait(false);
        return todas
            .Select(f => new FormaDePagamentoResumo(
                f.Id, f.Nome, f.Tipo.ToString(), f.TaxaPercentual, f.PrazoCompensacaoDias, f.ContaLiquidacaoId, f.Ativo))
            .ToList();
    }
}
