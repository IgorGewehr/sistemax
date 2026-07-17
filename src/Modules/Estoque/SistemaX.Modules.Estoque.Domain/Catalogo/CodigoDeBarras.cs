namespace SistemaX.Modules.Estoque.Domain.Catalogo;

/// <summary>EAN-13 (unidade), DUN-14 (caixa fechada) ou código interno de balança —
/// <see cref="Produto"/> guarda uma LISTA (supera o <c>barcode</c> único de referências antigas:
/// o mesmo item pode ser escaneado pela unidade ou pela caixa).</summary>
public enum TipoCodigoBarras { Ean13, Dun14, Interno }

public sealed record CodigoDeBarras(string Valor, TipoCodigoBarras Tipo);
