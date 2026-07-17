namespace SistemaX.Modules.Fiscal.Domain.Regimes;

/// <summary>
/// Classificação tributária da empresa perante o Fisco — muda raramente (migração de
/// faixa/opção anual). NÃO carrega alíquota nem CSOSN nenhum — só identifica QUAL conjunto de
/// regras (<c>RegraFiscalPorOperacao</c>) se aplica. Fechado de propósito: os 5 regimes são um
/// fato legal do Brasil, não um conceito de negócio que o tenant "inventa" — estender para um
/// regime novo é adicionar um valor de enum + popular <c>RegraFiscalPorOperacao</c>/
/// <c>PerfilFiscalNCM</c> para ele, nunca reescrever o motor de cálculo (ver
/// docs/fiscal/arquitetura.md §1/§2.1).
/// </summary>
public enum RegimeTributario
{
    Mei,
    SimplesNacional,

    /// <summary>Excesso de sublimite de receita bruta — ainda optante do Simples Nacional
    /// (continua recolhendo IRPJ/CSLL/PIS/COFINS/CPP unificados no DAS), mas ICMS/ISS são
    /// recolhidos "por fora" do DAS, PELAS REGRAS DO REGIME NORMAL (CRT=2 na NF-e — mas o campo
    /// ICMS do item usa CST, tabela B, igual a LucroPresumido/LucroReal — NUNCA CSOSN, ver
    /// <see cref="RegimeTributarioExtensions.UsaCsosn"/>).</summary>
    SimplesNacionalSublimite,

    LucroPresumido,

    /// <summary>Preparado, não operante nesta fase (nenhum PerfilFiscalNCM/RegraFiscalPorOperacao
    /// semeada para ele ainda) — mas o modelo já comporta sem refatoração: mesmo enum, mesma
    /// tabela de regras, só faltam as linhas de dado quando o primeiro tenant precisar.</summary>
    LucroReal
}

/// <summary>
/// Único conhecimento "hardcoded" aceitável do regime — fatos fechados do layout SEFAZ, nunca
/// dado editável por UF/tenant (mesma classe de exceção que <c>OrigemMercadoriaExtensions</c>).
/// </summary>
public static class RegimeTributarioExtensions
{
    /// <summary>Código de Regime Tributário do cabeçalho da NF-e/NFC-e — fato fechado do layout
    /// SEFAZ (3 valores, ponto), nunca duplicado inline em cada lugar que monta o XML.</summary>
    public static string Crt(this RegimeTributario regime) => regime switch
    {
        RegimeTributario.Mei or RegimeTributario.SimplesNacional => "1",
        RegimeTributario.SimplesNacionalSublimite => "2",
        RegimeTributario.LucroPresumido or RegimeTributario.LucroReal => "3",
        _ => throw new ArgumentOutOfRangeException(nameof(regime))
    };

    /// <summary>Só o Simples Nacional "pleno" (MEI e SimplesNacional, CRT=1) usa CSOSN.
    /// <see cref="RegimeTributario.SimplesNacionalSublimite"/> (CRT=2) usa CST — tabela B, igual
    /// ao regime Normal — porque o excesso de sublimite tira exatamente o ICMS/ISS do tratamento
    /// simplificado. É esta função — não um switch solto em cada lugar que monta um item — que
    /// decide isso (docs/fiscal/arquitetura.md §2.1).</summary>
    public static bool UsaCsosn(this RegimeTributario regime) =>
        regime is RegimeTributario.Mei or RegimeTributario.SimplesNacional;
}
