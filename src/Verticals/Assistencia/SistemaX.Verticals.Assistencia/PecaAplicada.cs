using SistemaX.SharedKernel;

namespace SistemaX.Verticals.Assistencia;

/// <summary>Peça efetivamente aplicada durante a execução — <see cref="Money"/> sempre em
/// centavos-inteiros (mesmo padrão de <c>ItemDeVenda</c>, Vendas.Domain). Carrega
/// <see cref="LinhaId"/> (estável — igual ao da <see cref="PecaOrcada"/> de origem quando
/// <see cref="Origem"/> é <see cref="OrigemPeca.Orcada"/>, novo ULID quando é
/// <see cref="OrigemPeca.Extra"/>) para que a chave de idempotência dos eventos de estoque
/// (<c>os.baixa:{osId}:{linhaId}</c>) nunca dependa de timestamp.</summary>
public sealed record PecaAplicada(
    string LinhaId, string? ProdutoId, string Descricao, int Quantidade, Money PrecoUnitario, OrigemPeca Origem)
{
    public Money Subtotal => PrecoUnitario * Quantidade;
}
