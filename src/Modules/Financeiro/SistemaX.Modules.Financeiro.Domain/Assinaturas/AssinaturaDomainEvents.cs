using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Assinaturas;

/// <summary>Nasceu uma assinatura → +MRR novo no mês.</summary>
public sealed record AssinaturaCriada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Inicio) : DomainEvent;

/// <summary>Assinatura cancelada = CHURN. Carrega o MRR perdido e o motivo (pra análise de causa).</summary>
public sealed record AssinaturaCancelada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Quando, string Motivo) : DomainEvent;

public sealed record AssinaturaPausada(string AssinaturaId) : DomainEvent;

public sealed record AssinaturaReativada(string AssinaturaId) : DomainEvent;
