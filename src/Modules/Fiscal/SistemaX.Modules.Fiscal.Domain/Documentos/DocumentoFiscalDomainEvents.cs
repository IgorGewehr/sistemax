using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Documentos;

/// <summary>Número alocado (ainda não transmitido) — auditoria/observabilidade, nunca gatilho de
/// outro fato de negócio. <see cref="ParaEventoDeIntegracao"/> é a ponte pura (sem side-effect)
/// para o contrato cross-módulo em <c>Modules.Abstractions</c> — a Application chama isto DEPOIS
/// do commit local, nunca antes (R3/R5 do CLAUDE.md).</summary>
public sealed record NumeroFiscalAlocadoDomainEvent(
    string DocumentoFiscalId, string TenantId, TipoDocumentoFiscal Tipo, string Serie, long Numero) : DomainEvent
{
    public NumeroFiscalAlocado ParaEventoDeIntegracao() =>
        new(DocumentoFiscalId, TenantId, Tipo.ToString(), Serie, Numero, OccurredOn);
}

/// <summary>Documento autorizado pela SEFAZ — notificações, futura Contabilidade/SPED.</summary>
public sealed record DocumentoFiscalAutorizadoDomainEvent(
    string DocumentoFiscalId, string TenantId, TipoDocumentoFiscal Tipo, string ChaveDeAcesso,
    string Serie, long Numero, Money Total, DateTimeOffset AutorizadoEm) : DomainEvent
{
    public DocumentoFiscalAutorizado ParaEventoDeIntegracao() =>
        new(DocumentoFiscalId, TenantId, Tipo.ToString(), ChaveDeAcesso, Serie, Numero, Total.Centavos, AutorizadoEm, OccurredOn);
}

/// <summary>Documento cancelado — nunca dispara reversão financeira sozinho (a <c>VendaEstornada</c>
/// já cobre o lado financeiro; este evento é só o lado fiscal do mesmo fato).</summary>
public sealed record DocumentoFiscalCanceladoDomainEvent(
    string DocumentoFiscalId, string TenantId, Money Total) : DomainEvent
{
    public DocumentoFiscalCancelado ParaEventoDeIntegracao() =>
        new(DocumentoFiscalId, TenantId, Total.Centavos, OccurredOn);
}

/// <summary>Número alocado que nunca chegou a autorizar — alimenta o job de protocolo de
/// Inutilização de Numeração na SEFAZ dentro do prazo legal (docs/fiscal/arquitetura.md §5).</summary>
public sealed record NumeroFiscalInutilizadoDomainEvent(
    string DocumentoFiscalId, string TenantId, TipoDocumentoFiscal Tipo, string Serie, long Numero, string Motivo) : DomainEvent
{
    public NumeroFiscalInutilizado ParaEventoDeIntegracao() =>
        new(DocumentoFiscalId, TenantId, Tipo.ToString(), Serie, Numero, Motivo, OccurredOn);
}

/// <summary>Documento entrou em contingência (NFC-e, <c>tpEmis=9</c>) — XML já assinado
/// localmente, DANFCE já impresso, aguardando rede para transmitir de verdade. Fecha o gap #8 de
/// docs/fiscal/emissao-mapping.md §6.2. Auditoria/observabilidade — nunca gatilho de outro fato
/// de negócio.</summary>
public sealed record DocumentoFiscalEmContingenciaDomainEvent(
    string DocumentoFiscalId, string TenantId, TipoDocumentoFiscal Tipo, string Serie, long Numero,
    DateTimeOffset DhCont, string Justificativa) : DomainEvent
{
    public DocumentoFiscalEmContingencia ParaEventoDeIntegracao() =>
        new(DocumentoFiscalId, TenantId, Tipo.ToString(), Serie, Numero, DhCont, Justificativa, OccurredOn);
}
