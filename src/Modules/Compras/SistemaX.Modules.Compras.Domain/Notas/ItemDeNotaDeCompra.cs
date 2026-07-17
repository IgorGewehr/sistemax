using SistemaX.Modules.Compras.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// Linha de uma <see cref="NotaDeCompra"/> — objeto de valor imutável (identidade é <see cref="NItem"/>,
/// o número de item da NF-e; "alterar" reconstrói via <c>with</c>, a mesma técnica de
/// <c>ItemDeVenda</c> do módulo Vendas). Carrega os valores BRUTOS extraídos do XML (ou digitados)
/// e, depois que o match é resolvido, também o resultado da conversão de unidade e — só depois do
/// recebimento — o custo de entrada CONGELADO (<see cref="CustoTotalEntrada"/>).
///
/// <see cref="QuantidadeConvertidaMilesimos"/> é DERIVADA de <see cref="QuantidadeNf"/> ×
/// <see cref="FatorConversaoAplicadoMilesimos"/> (ambos em milésimos, por isso a divisão por 1000)
/// — nunca armazenada separadamente, para nunca dessincronizar do fator aplicado.
/// </summary>
public sealed record ItemDeNotaDeCompra
{
    // `init` (não só `get`) em TODAS as propriedades: é o que habilita `with` reconstruir a linha
    // (ComMatch/ComoIgnorado/ComCustoDeEntrada) — record com construtor explícito não gera
    // acessor de clonagem sozinho, precisa que cada propriedade declare `init`.
    public int NItem { get; init; }
    public string? CProd { get; init; }
    public string DescricaoNf { get; init; }
    public string? Ncm { get; init; }
    public string UnidadeNf { get; init; }
    public Quantidade QuantidadeNf { get; init; }
    public Money VProd { get; init; }
    public Money VDesc { get; init; }
    public Money? VFreteItem { get; init; }
    public Money? VSegItem { get; init; }
    public Money? VOutroItem { get; init; }
    public Money VIpi { get; init; }
    public Money VIcmsSt { get; init; }
    public string? LoteFornecedor { get; init; }
    public DateOnly? Validade { get; init; }

    public MatchState MatchState { get; init; }
    public string? ProdutoId { get; init; }
    public long? FatorConversaoAplicadoMilesimos { get; init; }

    /// <summary>Custo de entrada (landed) TOTAL da linha — só existe depois de
    /// <see cref="NotaDeCompra.ConfirmarRecebimento"/> congelar o rateio de <see cref="CustoDeEntrada"/>.
    /// O custo UNITÁRIO nunca é persistido (deriva deste total ÷ quantidade na leitura — plano §6.1).</summary>
    public Money? CustoTotalEntrada { get; init; }

    public Quantidade? QuantidadeConvertida => FatorConversaoAplicadoMilesimos is { } fator
        ? new Quantidade(QuantidadeNf.Milesimos * fator / 1000L)
        : null;

    private ItemDeNotaDeCompra(
        int nItem, string? cProd, string descricaoNf, string? ncm, string unidadeNf, Quantidade quantidadeNf,
        Money vProd, Money vDesc, Money? vFreteItem, Money? vSegItem, Money? vOutroItem, Money vIpi, Money vIcmsSt,
        MatchState matchState, string? produtoId, long? fatorConversaoAplicadoMilesimos,
        string? loteFornecedor, DateOnly? validade, Money? custoTotalEntrada)
    {
        NItem = nItem;
        CProd = cProd;
        DescricaoNf = descricaoNf;
        Ncm = ncm;
        UnidadeNf = unidadeNf;
        QuantidadeNf = quantidadeNf;
        VProd = vProd;
        VDesc = vDesc;
        VFreteItem = vFreteItem;
        VSegItem = vSegItem;
        VOutroItem = vOutroItem;
        VIpi = vIpi;
        VIcmsSt = vIcmsSt;
        MatchState = matchState;
        ProdutoId = produtoId;
        FatorConversaoAplicadoMilesimos = fatorConversaoAplicadoMilesimos;
        LoteFornecedor = loteFornecedor;
        Validade = validade;
        CustoTotalEntrada = custoTotalEntrada;
    }

    public static Result<ItemDeNotaDeCompra> Criar(
        int nItem, string? cProd, string descricaoNf, string? ncm, string unidadeNf, Quantidade quantidadeNf,
        Money vProd, Money vDesc, Money? vFreteItem, Money? vSegItem, Money? vOutroItem, Money vIpi, Money vIcmsSt,
        MatchState matchState, string? produtoId, long? fatorConversaoAplicadoMilesimos,
        string? loteFornecedor = null, DateOnly? validade = null)
    {
        if (!quantidadeNf.EhPositiva)
            return Result.Falhar<ItemDeNotaDeCompra>(new Error("compras.item.quantidade_invalida", $"Item {nItem}: quantidade deve ser maior que zero."));

        if (!vProd.EhPositivo)
            return Result.Falhar<ItemDeNotaDeCompra>(new Error("compras.item.vprod_invalido", $"Item {nItem}: valor do produto deve ser positivo."));

        if (matchState is MatchState.Auto or MatchState.Manual && string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar<ItemDeNotaDeCompra>(new Error("compras.item.produto_ausente", $"Item {nItem}: match resolvido exige ProdutoId."));

        return Result.Ok(new ItemDeNotaDeCompra(
            nItem, cProd, descricaoNf, ncm, unidadeNf, quantidadeNf, vProd, vDesc, vFreteItem, vSegItem, vOutroItem,
            vIpi, vIcmsSt, matchState, produtoId, fatorConversaoAplicadoMilesimos, loteFornecedor, validade, custoTotalEntrada: null));
    }

    /// <summary>Aplica o resultado de um match (cascata automática ou resolução humana) — nunca
    /// chamado depois do recebimento (guarda vive em <see cref="NotaDeCompra"/>, que controla o
    /// FSM).</summary>
    internal ItemDeNotaDeCompra ComMatch(MatchState matchState, string produtoId, long fatorConversaoAplicadoMilesimos)
        => this with { MatchState = matchState, ProdutoId = produtoId, FatorConversaoAplicadoMilesimos = fatorConversaoAplicadoMilesimos };

    internal ItemDeNotaDeCompra ComoIgnorado() => this with { MatchState = MatchState.Ignorado };

    internal ItemDeNotaDeCompra ComCustoDeEntrada(Money custoTotalEntrada) => this with { CustoTotalEntrada = custoTotalEntrada };
}
