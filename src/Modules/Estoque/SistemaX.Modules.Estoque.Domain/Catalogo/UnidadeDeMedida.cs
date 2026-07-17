namespace SistemaX.Modules.Estoque.Domain.Catalogo;

/// <summary>Unidades suportadas — herdado do catálogo de referência do ERP (saas-erp).</summary>
public enum UnidadeDeMedida { UN, KG, L, M, M2, M3, CX, PCT }

public static class UnidadeDeMedidaExtensions
{
    /// <summary>UN/CX/PCT só aceitam milésimos múltiplos de 1_000 (não fraciona "0,5 caixa"
    /// numa venda comum); KG/L/M/M2/M3 aceitam fração.</summary>
    public static bool PermiteFracao(this UnidadeDeMedida unidade) => unidade is
        UnidadeDeMedida.KG or UnidadeDeMedida.L or UnidadeDeMedida.M or UnidadeDeMedida.M2 or UnidadeDeMedida.M3;
}
