namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary>
/// <code>
///   Rascunho ──ResolverItens() falha──► BloqueadoPorConfiguracaoFiscal ──corrige cadastro──► Rascunho
///   Rascunho ──ResolverItens() ok + AlocarNumero()──► NumeroAlocado
///   NumeroAlocado ──Transmitir()──► Autorizado | Denegado | Rejeitado
///   Rejeitado ──Retransmitir() (mesmo número)──► Autorizado | Denegado | Rejeitado
///   NumeroAlocado ──Desistir()──► Inutilizado   (nunca chegou a transmitir)
///   Rejeitado ──Desistir()──► Inutilizado        (desistiu sem nunca autorizar)
///   Autorizado ──Cancelar()──► Cancelado
///   NumeroAlocado ──PrepararContingencia() (rede caiu no PDV)──► EmContingencia
///   EmContingencia ──Transmitir() (rede voltou)──► Autorizado | Denegado | Rejeitado
///   EmContingencia ──Transmitir() rejeitado──► EmContingencia (self-loop — PRESERVA, nunca cai
///     pra terminal antes da janela de 24h expirar, mesmo racional do fiscalDocument.ts do saas-erp)
/// </code>
/// Denegado, Cancelado e Inutilizado são terminais. NÃO existe transição de "corrigir e reemitir
/// com o MESMO status Autorizado" — um documento autorizado é imutável; qualquer correção
/// pós-autorização é OUTRO documento (Carta de Correção para erro leve, ou nova nota +
/// cancelamento/devolução para erro grave) — ver docs/fiscal/arquitetura.md §2.6.
///
/// <see cref="EmContingencia"/> fecha o gap #8 de docs/fiscal/emissao-mapping.md §6.2: um XML já
/// assinado localmente (tpEmis=9) com DANFCE já impresso é um fato legal irreversível — NUNCA
/// aceita <c>Desistir()</c> (→ <see cref="Inutilizado"/>) como transição válida, propositalmente
/// ausente da tabela de transições deste status (o cliente já está com o comprovante em mão).
/// </summary>
public enum StatusDocumentoFiscal
{
    Rascunho,
    BloqueadoPorConfiguracaoFiscal,
    NumeroAlocado,
    Autorizado,
    Denegado,
    Rejeitado,
    Cancelado,
    Inutilizado,
    EmContingencia
}
