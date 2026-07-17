using SistemaX.Modules.Estoque.Domain.Comum;

namespace SistemaX.Modules.Estoque.Domain.Catalogo;

/// <summary>
/// Linha da ficha técnica (BOM): quanto de <see cref="ProdutoInsumoId"/> um produto composto
/// consome POR UNIDADE vendida. Um produto com <c>FichaTecnica</c> não tem saldo próprio — a
/// baixa expande recursivamente nos insumos-folha (<c>ExpansorDeFichaTecnica</c>, Application).
/// </summary>
public sealed record ComponenteDeFicha(string ProdutoInsumoId, Quantidade Quantidade);
