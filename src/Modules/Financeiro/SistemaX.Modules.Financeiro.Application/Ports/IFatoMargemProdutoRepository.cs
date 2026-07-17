using SistemaX.Modules.Financeiro.Application.Analitico;

namespace SistemaX.Modules.Financeiro.Application.Ports;

/// <summary>Item de uma venda com sua receita já calculada, ainda SEM custo alocado — o estado de
/// transição entre <c>VendaItensMovimentados</c> (quando a receita por produto já é conhecida) e
/// <c>CustoBaixadoPorVenda</c> (quando o custo total da venda chega, mas só no agregado). Ver
/// <see cref="IFatoMargemProdutoRepository.RegistrarItensDeVendaAsync"/>.</summary>
public sealed record ItemMargemPendente(string ProdutoId, long ReceitaItemCentavos);

/// <summary>
/// Port da fact table <c>fato_margem_produto</c> — read-model foldado do ledger por
/// <see cref="FatoMargemProdutoProjection"/>. Dividido em DUAS escritas (nunca um "acumular"
/// único, ao contrário de <see cref="IFatoReceitaDiariaRepository"/>) porque receita e custo
/// nascem de EVENTOS DIFERENTES, em momentos diferentes, e o custo só pode ser repartido por
/// produto quando sabemos quais produtos entraram naquela venda especificamente — daí o estado de
/// transição via <see cref="RegistrarItensDeVendaAsync"/>/<see cref="AlocarCustoDeVendaAsync"/>.
/// </summary>
public interface IFatoMargemProdutoRepository
{
    /// <summary>
    /// Chamado pelo fold ao processar <c>VendaItensMovimentados</c>: (1) acumula
    /// <see cref="FatoMargemProduto.ReceitaCentavos"/> por produto/dia imediatamente (a receita já é
    /// conhecida neste evento) e (2) guarda a quebra por produto num estado de transição
    /// (chave tenant+venda), para quando <see cref="AlocarCustoDeVendaAsync"/> chegar depois com o
    /// custo total da MESMA venda. Idempotente: reprocessar a mesma venda com os mesmos itens
    /// produz o mesmo estado acumulado (mesmo contrato de <c>IProjection.AplicarAsync</c>).
    /// </summary>
    Task RegistrarItensDeVendaAsync(
        string tenantId, string vendaId, DateOnly dia, IReadOnlyList<ItemMargemPendente> itens, CancellationToken ct = default);

    /// <summary>
    /// Chamado pelo fold ao processar <c>CustoBaixadoPorVenda</c>: rateia
    /// <paramref name="custoTotalCentavos"/> entre os itens pendentes da MESMA venda (proporcional
    /// à receita de cada item — ver <see cref="Quant.RateioProporcional"/>), acumula
    /// <see cref="FatoMargemProduto.CustoCentavos"/> por produto/dia, e CONSOME (apaga) o estado de
    /// transição daquela venda — não é preciso mantê-lo depois de alocado. Se não houver itens
    /// pendentes para a venda (nunca deveria acontecer na ordem normal do ledger — ver XML doc de
    /// <c>FatoMargemProdutoProjection</c> —, mas replay/estado corrompido são sempre possíveis), é
    /// no-op silencioso: o custo fica sem fold, documentado como perda aceitável desta F1.
    /// </summary>
    Task AlocarCustoDeVendaAsync(string tenantId, string vendaId, long custoTotalCentavos, CancellationToken ct = default);

    Task<FatoMargemProduto?> ObterAsync(string tenantId, string produtoId, DateOnly dia, CancellationToken ct = default);

    Task<IReadOnlyList<FatoMargemProduto>> ListarAsync(string tenantId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    Task<IReadOnlyList<FatoMargemProduto>> ListarPorProdutoAsync(string tenantId, string produtoId, DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Apaga TODA a fact table (inclusive o estado de transição pendente) — primeiro passo
    /// de um replay do zero (<c>IProjection.ResetarAsync</c>).</summary>
    Task ZerarTudoAsync(CancellationToken ct = default);
}
