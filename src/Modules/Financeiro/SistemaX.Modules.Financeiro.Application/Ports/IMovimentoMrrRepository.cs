using SistemaX.Modules.Financeiro.Application.Mrr;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Persistência do ledger de <see cref="MovimentoMrr"/> (P1-4) — append-only, nunca
/// atualizado/apagado (é histórico de fatos, mesma filosofia de <c>LancamentoContabil</c>).</summary>
public interface IMovimentoMrrRepository
{
    Task RegistrarAsync(MovimentoMrr movimento, CancellationToken ct = default);

    /// <summary>Todos os movimentos do tenant — a agregação (por competência, cumulativa etc.)
    /// acontece na Application (<c>PainelDeMovimentosMrrService</c>), não aqui: mesmo padrão de
    /// <c>ReceitaRecorrenteService</c> sobre <c>IAssinaturaRepository.ListarAsync</c>.</summary>
    Task<IReadOnlyList<MovimentoMrr>> ListarAsync(string businessId, CancellationToken ct = default);
}
