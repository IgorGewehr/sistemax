using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Domain;

/// <summary>
/// Linha de venda — objeto de valor (sem identidade de negócio própria, vive só dentro de uma
/// <see cref="Venda"/>). Carrega um <see cref="Id"/> ULID só para endereçamento estável dentro da
/// lista (remover/descontar/alterar quantidade UMA linha específica sem depender de índice — a
/// mesma lição de UI documentada em <see cref="PagamentoDeVenda"/>). <see cref="PrecoUnitario"/> e
/// <see cref="Desconto"/> já são <see cref="Money"/>: nunca <c>decimal</c>/<c>double</c> cru, mesmo
/// numa linha "simples" (regra dura do projeto).
///
/// É imutável: "alterar" quantidade ou desconto sempre reconstrói o record com <see cref="Id"/>
/// preservado (<see cref="ComQuantidade"/>/<see cref="ComDesconto"/>) — <see cref="Venda"/> troca a
/// entrada correspondente na sua lista interna.
/// </summary>
public sealed record ItemDeVenda
{
    public string Id { get; }
    public string ProdutoId { get; }
    public string Descricao { get; }
    public int Quantidade { get; }
    public Money PrecoUnitario { get; }
    public Money Desconto { get; }

    /// <summary>Preço × quantidade, ANTES do desconto do item — teto para <see cref="Desconto"/>
    /// e base do rateio fiscal futuro (a NFC-e precisa do bruto e do desconto separados).</summary>
    public Money SubtotalBruto => PrecoUnitario * Quantidade;

    /// <summary>Subtotal líquido — o que de fato compõe o total da venda.</summary>
    public Money Subtotal => SubtotalBruto - Desconto;

    private ItemDeVenda(string id, string produtoId, string descricao, int quantidade, Money precoUnitario, Money desconto)
    {
        Id = id;
        ProdutoId = produtoId;
        Descricao = descricao;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
        Desconto = desconto;
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida (o item já passou pela validação de Criar/ComQuantidade/ComDesconto quando foi originalmente adicionado à venda).</summary>
    public static ItemDeVenda Reconstituir(string id, string produtoId, string descricao, int quantidade, Money precoUnitario, Money desconto)
        => new(id, produtoId, descricao, quantidade, precoUnitario, desconto);

    public static Result<ItemDeVenda> Criar(string produtoId, string descricao, int quantidade, Money precoUnitario)
    {
        if (string.IsNullOrWhiteSpace(produtoId))
            return Result.Falhar<ItemDeVenda>(new Error("venda.item.produto_invalido", "ProdutoId é obrigatório."));

        if (quantidade <= 0)
            return Result.Falhar<ItemDeVenda>(new Error("venda.quantidade_invalida", "Quantidade deve ser maior que zero."));

        if (!precoUnitario.EhPositivo)
            return Result.Falhar<ItemDeVenda>(new Error("venda.item.preco_invalido", "Preço unitário deve ser positivo."));

        return Result.Ok(new ItemDeVenda(Ulid.NewUlid().ToString(), produtoId, descricao, quantidade, precoUnitario, Money.Zero));
    }

    internal Result<ItemDeVenda> ComQuantidade(int novaQuantidade)
    {
        if (novaQuantidade <= 0)
            return Result.Falhar<ItemDeVenda>(new Error(
                "venda.quantidade_invalida", "Quantidade deve ser maior que zero — para remover o item, use RemoverItem."));

        var novoSubtotalBruto = PrecoUnitario * novaQuantidade;
        if (Desconto.Centavos > novoSubtotalBruto.Centavos)
            return Result.Falhar<ItemDeVenda>(new Error(
                "venda.item.desconto_maior_que_subtotal",
                "Desconto do item não pode ser maior que o novo subtotal bruto do item."));

        return Result.Ok(new ItemDeVenda(Id, ProdutoId, Descricao, novaQuantidade, PrecoUnitario, Desconto));
    }

    internal Result<ItemDeVenda> ComDesconto(Money desconto)
    {
        if (desconto.EhNegativo)
            return Result.Falhar<ItemDeVenda>(new Error("venda.item.desconto_negativo", "Desconto não pode ser negativo."));

        if (desconto.Centavos > SubtotalBruto.Centavos)
            return Result.Falhar<ItemDeVenda>(new Error(
                "venda.item.desconto_maior_que_subtotal",
                "Desconto do item não pode ser maior que o subtotal bruto do item."));

        return Result.Ok(new ItemDeVenda(Id, ProdutoId, Descricao, Quantidade, PrecoUnitario, desconto));
    }
}
