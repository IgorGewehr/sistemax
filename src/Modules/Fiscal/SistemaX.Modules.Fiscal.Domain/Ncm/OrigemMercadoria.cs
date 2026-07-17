namespace SistemaX.Modules.Fiscal.Domain.Ncm;

/// <summary>
/// Origem da mercadoria — tabela oficial fechada do Manual de Orientação do Contribuinte da NF-e
/// (grupo ICMS, tag <c>&lt;orig&gt;</c> do XML), campo OBRIGATÓRIO em todo item, independente de
/// regime. Nunca inferido: entra por cadastro (NCM ou produto) e é carregado até
/// <c>TributoResolvidoItem</c> sem re-inferência. Fechado por norma federal do layout NF-e (mesma
/// classe de "hardcode aceitável" que <c>RegimeTributarioExtensions.Crt</c>) — não é dado que o
/// tenant inventa.
/// </summary>
public enum OrigemMercadoria
{
    Nacional = 0,
    EstrangeiraImportacaoDireta = 1,
    EstrangeiraAdquiridaMercadoInterno = 2,
    NacionalConteudoImportacaoSuperior40 = 3,
    NacionalProcessoProdutivoBasico = 4,
    NacionalConteudoImportacaoAteQuarenta = 5,
    EstrangeiraImportacaoDiretaSemSimilarNacional = 6,
    EstrangeiraAdquiridaMercadoInternoSemSimilarNacional = 7,
    NacionalConteudoImportacaoSuperior70 = 8
}

/// <summary>
/// Única regra "hardcoded" aceitável de <see cref="OrigemMercadoria"/>: a Resolução do Senado
/// Federal 13/2012 fixa em 4% a alíquota de ICMS interestadual para mercadoria importada,
/// SUBSTITUINDO a alíquota interestadual que <c>RegraFiscalPorOperacao</c> traria para aquela UF —
/// fato fechado de lei federal, nunca dado editável por UF (docs/fiscal/arquitetura.md §2.4/§3).
/// </summary>
public static class OrigemMercadoriaExtensions
{
    public static bool ForcaAliquotaInterestadual4Pct(this OrigemMercadoria origem) =>
        origem is OrigemMercadoria.EstrangeiraImportacaoDireta
               or OrigemMercadoria.EstrangeiraAdquiridaMercadoInterno
               or OrigemMercadoria.NacionalConteudoImportacaoSuperior40
               or OrigemMercadoria.EstrangeiraImportacaoDiretaSemSimilarNacional
               or OrigemMercadoria.EstrangeiraAdquiridaMercadoInternoSemSimilarNacional;
}
