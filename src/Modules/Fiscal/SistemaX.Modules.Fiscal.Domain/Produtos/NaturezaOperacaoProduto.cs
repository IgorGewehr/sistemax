namespace SistemaX.Modules.Fiscal.Domain.Produtos;

/// <summary>
/// Fecha o gap de CFOP identificado em docs/fiscal/arquitetura.md §2.3/§9: <c>5101</c>/<c>6101</c>
/// (venda de produção própria) e <c>5102</c>/<c>6102</c> (venda de mercadoria de terceiros para
/// revenda) têm o MESMO <c>TipoOperacaoFiscal</c>/<c>EhInterestadual</c>/
/// <c>DestinatarioContribuinteIcms</c> — o que diferencia é este atributo do PRODUTO (decisão de
/// Igor, ver ADR-0002 revisão de fechamento do gap CFOP).
///
/// É um atributo do PRODUTO (cadastrado no Estoque, propagado via evento de integração
/// <c>ProdutoFiscalAtualizado</c>/<c>ProdutoFiscalAtualizadoEmLote</c> — <c>Modules.Abstractions</c>
/// — e cacheado localmente em <c>DadosFiscaisProdutoCache</c>), NUNCA um dado só do Fiscal: quem
/// fabrica ou revende o produto é fato do catálogo, mesma fronteira já usada para NCM/CEST
/// (docs/fiscal/arquitetura.md §4). Cada módulo mantém sua PRÓPRIA cópia deste enum (mesma regra
/// de fronteira de <c>SourceRef</c>/<c>Quantidade</c>) — o valor atravessa o evento de integração
/// como STRING estável (<see cref="NaturezaOperacaoProdutoExtensions"/>), nunca por ordinal
/// (ordinal quebraria se os dois enums evoluíssem em ordens diferentes nos dois módulos).
/// </summary>
public enum NaturezaOperacaoProduto
{
    ProducaoPropria,
    RevendaDeTerceiros,
    ImportacaoPropria
}

public static class NaturezaOperacaoProdutoExtensions
{
    public const string CodigoProducaoPropria = "producao_propria";
    public const string CodigoRevendaDeTerceiros = "revenda_terceiros";
    public const string CodigoImportacaoPropria = "importacao_propria";

    /// <summary>Código estável de wire (evento de integração/coluna SQLite) — nunca o ordinal do
    /// enum, que não é garantia de estabilidade entre módulos versionados independentemente.</summary>
    public static string ParaCodigo(this NaturezaOperacaoProduto natureza) => natureza switch
    {
        NaturezaOperacaoProduto.ProducaoPropria => CodigoProducaoPropria,
        NaturezaOperacaoProduto.RevendaDeTerceiros => CodigoRevendaDeTerceiros,
        NaturezaOperacaoProduto.ImportacaoPropria => CodigoImportacaoPropria,
        _ => throw new ArgumentOutOfRangeException(nameof(natureza))
    };

    /// <summary>Default são "revenda de terceiros" — caso mais comum do varejo quando o produto
    /// ainda não foi classificado explicitamente (nunca "produção própria" silencioso, que teria
    /// implicação tributária mais favorável e não deve ser assumida).</summary>
    public static NaturezaOperacaoProduto DeCodigo(string? codigo) => codigo switch
    {
        CodigoProducaoPropria => NaturezaOperacaoProduto.ProducaoPropria,
        CodigoImportacaoPropria => NaturezaOperacaoProduto.ImportacaoPropria,
        _ => NaturezaOperacaoProduto.RevendaDeTerceiros
    };
}
