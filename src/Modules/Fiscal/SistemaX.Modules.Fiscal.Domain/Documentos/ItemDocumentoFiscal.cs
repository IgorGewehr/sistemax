using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Documentos;

public sealed record ItemDocumentoFiscal(
    string ProdutoId,
    string Descricao,
    string Ncm,
    string? Cest,
    OrigemMercadoria Origem,
    string Cfop,
    Quantidade Quantidade,
    Money PrecoUnitario,
    Money Desconto,
    IReadOnlyList<TributoResolvidoItem> Tributos)
{
    /// <summary>Quantidade fracionária multiplica em REAIS (não em centavos-inteiros — Money não
    /// expõe operador para fator decimal, R1 do CLAUDE.md), com o mesmo arredondamento bancário de
    /// <c>Money.DeReais</c> usado em todo o resto do módulo.</summary>
    public Money Subtotal => Money.DeReais(PrecoUnitario.EmReais * Quantidade.EmDecimal) - Desconto;
}
