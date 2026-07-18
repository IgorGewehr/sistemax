using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Port da fact table <c>fato_receita_diaria</c> — read-model foldado do ledger por
/// <see cref="FatoReceitaDiariaProjection"/>. <see cref="AcumularAsync"/> é a única escrita:
/// soma (ou subtrai, com delta negativo) sobre o valor já acumulado do dia+corrente — nunca um
/// "set" absoluto, porque o fold processa um evento de cada vez. Chave é (tenant, dia, corrente) —
/// P0-1, docs/financeiro/revisao-domain-fit-cnpj.md; ver <see cref="FatoReceitaDiaria"/>.</summary>
public interface IFatoReceitaDiariaRepository
{
    /// <summary><paramref name="projetoId"/> — P5: sentinela <c>""</c> (default) = sem projeto,
    /// nunca <c>null</c> (é parte da chave — ver <see cref="FatoReceitaDiaria"/>).</summary>
    Task AcumularAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, long deltaCentavos, string projetoId = "", CancellationToken ct = default);

    Task<FatoReceitaDiaria?> ObterAsync(string tenantId, DateOnly dia, CorrenteDeReceita corrente, string projetoId = "", CancellationToken ct = default);

    /// <summary>Retorna uma linha POR CORRENTE presente no período — quem só quer o total do dia
    /// soma todas as linhas retornadas (o total nunca muda de valor, só de granularidade).</summary>
    Task<IReadOnlyList<FatoReceitaDiaria>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Apaga TODA a fact table — primeiro passo de um replay do zero
    /// (<c>IProjection.ResetarAsync</c>).</summary>
    Task ZerarTudoAsync(CancellationToken ct = default);
}
