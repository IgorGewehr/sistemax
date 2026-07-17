using SistemaX.Modules.Financeiro.Application.Analitico;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port da fact table <c>fato_caixa_diario</c> — read-model foldado do ledger por
/// <see cref="FatoCaixaDiarioProjection"/>. Entradas e saídas acumulam independentemente; o saldo
/// do dia é sempre derivado na leitura (<see cref="FatoCaixaDiario.SaldoDiaCentavos"/>).</summary>
public interface IFatoCaixaDiarioRepository
{
    Task AcumularEntradaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default);

    Task AcumularSaidaAsync(string tenantId, DateOnly dia, long deltaCentavos, CancellationToken ct = default);

    Task<FatoCaixaDiario?> ObterAsync(string tenantId, DateOnly dia, CancellationToken ct = default);

    Task<IReadOnlyList<FatoCaixaDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Apaga TODA a fact table — primeiro passo de um replay do zero
    /// (<c>IProjection.ResetarAsync</c>).</summary>
    Task ZerarTudoAsync(CancellationToken ct = default);
}
