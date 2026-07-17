using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Domain.Regras;

/// <summary>
/// CFOP PADRÃO configurável — a camada "padrão-config" da cadeia de resolução de CFOP decidida
/// por Igor (ver ADR-0002, fechamento do gap §2.3/§9): <c>emissão &gt; produto &gt; padrão-config</c>.
/// Nunca hardcode — dado seedável e editável em runtime (viria de Settings→Fiscal, mesma tela das
/// demais tributações), resolvido por <c>IResolvedorDeCfop</c> (Application) contra esta tabela.
///
/// Chave = (<see cref="TipoOperacao"/>, <see cref="EhInterestadual"/>,
/// <see cref="DestinatarioContribuinteIcms"/>, <see cref="Natureza"/>) — <see cref="Natureza"/> é
/// o que finalmente distingue <c>5101</c>/<c>6101</c> (produção própria) de <c>5102</c>/<c>6102</c>
/// (revenda de terceiros), o gap que travava a resolução de CFOP antes desta decisão.
/// <see cref="TenantId"/> nullable segue a MESMA convenção de <see cref="RegraFiscalPorOperacao"/>:
/// linha sem tenant é o default do sistema; linha com tenant sobrepõe só para aquele tenant.
/// </summary>
public sealed record RegraCfop(
    string? TenantId,
    TipoOperacaoFiscal TipoOperacao,
    bool EhInterestadual,
    bool DestinatarioContribuinteIcms,
    NaturezaOperacaoProduto Natureza,
    string Cfop)
{
    /// <summary>Mesmo critério de desempate de <see cref="RegraFiscalPorOperacao.Especificidade"/>
    /// — usado pelo resolvedor quando mais de uma linha bate na mesma chave.</summary>
    public int Especificidade => TenantId is not null ? 1 : 0;
}
