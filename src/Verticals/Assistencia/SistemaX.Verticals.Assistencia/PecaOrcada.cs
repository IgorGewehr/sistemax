using SistemaX.SharedKernel;

namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// Linha de peça PREVISTA dentro de um <see cref="Orcamento"/> — existe para que o cliente
/// aprove peças e mão de obra JUNTOS (gap do código original: orçamento só tinha mão de obra,
/// peça só entrava na execução → o valor aprovado não era o valor cobrado, briga na entrega).
///
/// <see cref="ProdutoId"/> nulo = peça sob encomenda / linha livre (ex.: "peça a definir") —
/// não tem efeito de estoque nenhum (não há o que reservar); ver <see cref="OrdemDeServico.RegistrarAprovacao"/>.
/// <see cref="LinhaId"/> é um ULID estável gerado quando a linha entra no orçamento — é essa
/// estabilidade que torna toda a cadeia reserva → baixa → estorno idempotente por construção.
/// </summary>
public sealed record PecaOrcada(string LinhaId, string? ProdutoId, string Descricao, int Quantidade, Money PrecoUnitario)
{
    public Money Subtotal => PrecoUnitario * Quantidade;

    public static PecaOrcada Nova(string? produtoId, string descricao, int quantidade, Money precoUnitario)
        => new(Ulid.NewUlid().ToString(), produtoId, descricao, quantidade, precoUnitario);
}
