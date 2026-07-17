using SistemaX.Modules.Financeiro.Application.Analitico;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port da fact table <c>fato_recebiveis</c> — APPEND-ONLY por construção (uma linha por
/// fato de origem, nunca update): <see cref="AdicionarAsync"/> é a única escrita.</summary>
public interface IFatoRecebiveisRepository
{
    Task AdicionarAsync(FatoRecebivel item, CancellationToken ct = default);

    /// <summary>Recebíveis com <see cref="FatoRecebivel.Vencimento"/> no período — "a receber por
    /// vencimento", a visão que a tela de recebíveis consome.</summary>
    Task<IReadOnlyList<FatoRecebivel>> ListarPorVencimentoAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Apaga TODA a fact table — primeiro passo de um replay do zero
    /// (<c>IProjection.ResetarAsync</c>).</summary>
    Task ZerarTudoAsync(CancellationToken ct = default);
}
