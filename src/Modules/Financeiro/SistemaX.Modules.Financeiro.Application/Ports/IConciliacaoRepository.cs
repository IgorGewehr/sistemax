using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Application.Ports;

public interface IConciliacaoRepository
{
    Task<Conciliacao?> ObterPorIdAsync(string id, CancellationToken ct = default);

    Task<Conciliacao?> BuscarPorParAsync(string movimentoFinanceiroId, string extratoBancarioItemId, CancellationToken ct = default);

    Task SalvarAsync(Conciliacao conciliacao, CancellationToken ct = default);

    /// <summary>Todos os vínculos (qualquer <see cref="StatusConciliacao"/>) de um tenant — usado
    /// pelo painel "Conciliação" da tela Bancário (docs/wiring/financeiro-telas-restantes.md §3)
    /// para separar, em memória, o que já bateu (<c>ConciliadoAuto</c>/<c>ConciliadoManual</c>) do
    /// que foi explicitamente descartado (<c>Ignorado</c>), sem precisar de N idas ao banco por
    /// par movimento/extrato.</summary>
    Task<IReadOnlyList<Conciliacao>> ListarPorBusinessIdAsync(string businessId, CancellationToken ct = default);
}

public interface IExtratoBancarioItemRepository
{
    Task<ExtratoBancarioItem?> BuscarPorIdentificadorExternoAsync(string businessId, string identificadorExterno, CancellationToken ct = default);

    Task SalvarAsync(ExtratoBancarioItem item, CancellationToken ct = default);

    Task<IReadOnlyList<ExtratoBancarioItem>> ListarNaoConciliadosAsync(string businessId, string contaBancariaCaixaId, CancellationToken ct = default);
}
