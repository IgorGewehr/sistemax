using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port da fact table <c>fato_custo_diario</c> — read-model foldado do ledger por
/// <see cref="FatoCustoDiarioProjection"/>. <see cref="AcumularAsync"/> é a única escrita: soma
/// (ou subtrai, com delta negativo) sobre o valor já acumulado do dia+corrente — nunca um "set"
/// absoluto, mesmo racional de <see cref="IFatoReceitaDiariaRepository"/>. Chave é
/// (tenant, dia, corrente) — P0-1.</summary>
public interface IFatoCustoDiarioRepository
{
    /// <summary><paramref name="projetoId"/> — P5: sentinela <c>""</c> (default) = sem projeto,
    /// nunca <c>null</c> (parte da chave — ver <see cref="FatoCustoDiario"/>).</summary>
    Task AcumularAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, long deltaCentavos, string projetoId = "", CancellationToken ct = default);

    Task<FatoCustoDiario?> ObterAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, string projetoId = "", CancellationToken ct = default);

    Task<IReadOnlyList<FatoCustoDiario>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Apaga TODA a fact table — primeiro passo de um replay do zero
    /// (<c>IProjection.ResetarAsync</c>).</summary>
    Task ZerarTudoAsync(CancellationToken ct = default);
}
