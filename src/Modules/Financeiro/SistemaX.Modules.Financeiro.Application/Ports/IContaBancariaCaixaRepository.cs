using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência de <see cref="ContaBancariaCaixa"/> — o LAR ÚNICO das contas/caixas que
/// <c>MovimentoFinanceiro.ContaBancariaCaixaId</c> referencia (docs/wiring/financeiro-telas-restantes.md §3).</summary>
public interface IContaBancariaCaixaRepository
{
    Task<ContaBancariaCaixa?> ObterPorIdAsync(string businessId, string id, CancellationToken ct = default);

    Task<IReadOnlyList<ContaBancariaCaixa>> ListarAsync(string businessId, bool apenasAtivas = false, CancellationToken ct = default);

    Task SalvarAsync(ContaBancariaCaixa conta, CancellationToken ct = default);
}
