namespace SistemaX.Modules.Compras.Domain.Comum;

/// <summary>
/// Referência ao fato de origem (módulo + id) — mesma forma e papel do <c>SourceRef</c> do
/// Financeiro/Estoque (cada módulo tem a sua cópia de propósito). Reservado para quando Compras
/// precisar referenciar o fato que originou um vínculo/nota (ex.: pedido de compra na fase 2).
/// </summary>
public sealed record SourceRef(string Modulo, string Id)
{
    public string Chave => $"{Modulo}:{Id}";

    public override string ToString() => Chave;
}
