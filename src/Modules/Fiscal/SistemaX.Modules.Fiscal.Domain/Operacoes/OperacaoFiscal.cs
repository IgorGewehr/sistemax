namespace SistemaX.Modules.Fiscal.Domain.Operacoes;

public enum TipoOperacaoFiscal
{
    VendaMercadoria,
    DevolucaoDeVenda,
    TransferenciaEntreEstabelecimentos,
    RemessaEmComodato,
    RemessaParaConserto,

    /// <summary>Reserva de extensão — NFS-e/ISS, fora de escopo desta fase (ver
    /// docs/fiscal/arquitetura.md §9).</summary>
    PrestacaoDeServico
}

/// <summary>
/// Contexto de uma operação concreta — o que, junto do NCM (via <c>PerfilFiscalNCM</c>/
/// <c>TributacaoProduto</c>) e do regime do tenant, alimenta a resolução de CFOP e de CSOSN/CST.
/// Nunca confundir com <c>DocumentoFiscal</c>: uma <see cref="OperacaoFiscal"/> é o "tipo de
/// fato" (venda interna? devolução? pra fora do estado?); o documento é o "registro" desse fato
/// já com número/chave.
/// </summary>
public sealed record OperacaoFiscal(
    TipoOperacaoFiscal Tipo,
    string UfOrigem,
    string UfDestino,
    bool DestinatarioConsumidorFinal,
    bool DestinatarioContribuinteIcms,
    bool OperacaoPresencial)
{
    public bool EhInterestadual => !string.Equals(UfOrigem, UfDestino, StringComparison.OrdinalIgnoreCase);

    /// <summary>Venda interestadual a consumidor final NÃO-contribuinte de ICMS — o caso clássico
    /// de e-commerce/B2C cross-UF que gera DIFAL + FCP além do ICMS de origem (EC 87/2015, ver
    /// docs/fiscal/arquitetura.md §2.2/§2.6/§3).</summary>
    public bool GeraPartilhaDifal => EhInterestadual && DestinatarioConsumidorFinal && !DestinatarioContribuinteIcms;
}
