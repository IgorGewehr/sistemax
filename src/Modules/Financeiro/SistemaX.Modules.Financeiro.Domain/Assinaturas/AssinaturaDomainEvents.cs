using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Assinaturas;

/// <summary>Nasceu uma assinatura → +MRR NOVO no mês.</summary>
public sealed record AssinaturaCriada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Inicio) : DomainEvent;

/// <summary>
/// Assinatura cancelada = CHURN. <see cref="MrrCentavos"/> é o MRR que de fato SAI da soma
/// corrente nesta transição — 0 se a assinatura já estava <see cref="StatusAssinatura.Pausada"/>
/// (o MRR já tinha saído via <see cref="AssinaturaPausada"/>; contar de novo aqui duplo-descontaria
/// o mesmo dinheiro do painel de movimentos — P1-4, docs/financeiro/revisao-domain-fit-cnpj.md).
/// </summary>
public sealed record AssinaturaCancelada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Quando, string Motivo) : DomainEvent;

/// <summary>P1-4 — MRR movement de CONTRAÇÃO (o MRR retirado, sempre o valor CHEIO da assinatura
/// no momento da pausa — não é churn, mas some da soma corrente até reativar).</summary>
public sealed record AssinaturaPausada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Quando) : DomainEvent;

/// <summary>P1-4 — MRR movement de REATIVAÇÃO (o MRR que volta a contar, saindo de
/// <see cref="StatusAssinatura.Pausada"/>).</summary>
public sealed record AssinaturaReativada(string AssinaturaId, string BusinessId, string ServicoId, long MrrCentavos, DateTimeOffset Quando) : DomainEvent;

/// <summary>P1-4 — troca de plano/valor (<see cref="Assinatura.AlterarValor"/>): MRR movement de
/// EXPANSÃO (<see cref="MrrNovoCentavos"/> &gt; <see cref="MrrAnteriorCentavos"/>) ou CONTRAÇÃO
/// (caso contrário) — a Application decide qual dos dois pelo sinal do delta, nunca o domínio.</summary>
public sealed record AssinaturaAlterada(
    string AssinaturaId, string BusinessId, string ServicoId, long MrrAnteriorCentavos, long MrrNovoCentavos, DateTimeOffset Quando) : DomainEvent;

/// <summary>P1-4 — dunning: cobrança vencida sem pagamento, assinatura entra em
/// <see cref="StatusAssinatura.Inadimplente"/>. NÃO é um MRR movement (ainda conta como corrente).</summary>
public sealed record AssinaturaMarcadaInadimplente(string AssinaturaId, string BusinessId, DateTimeOffset Quando) : DomainEvent;

/// <summary>P1-4 — dunning: cobrança em atraso liquidada antes da graça expirar, assinatura volta
/// a <see cref="StatusAssinatura.Ativa"/>. NÃO é um MRR movement (nunca saiu da soma corrente).</summary>
public sealed record AssinaturaRegularizada(string AssinaturaId, string BusinessId, DateTimeOffset Quando) : DomainEvent;

/// <summary>Análise por Projeto (docs/financeiro/design-analise-por-projeto.md §6.1) — a assinatura
/// foi tagueada/re-tagueada/desvinculada de um projeto (<see cref="Assinatura.VincularProjeto"/>).
/// NÃO é um MRR movement — classificação gerencial não move dinheiro.</summary>
public sealed record AssinaturaVinculadaAProjeto(string AssinaturaId, string BusinessId, string? ProjetoId, DateTimeOffset Quando) : DomainEvent;
