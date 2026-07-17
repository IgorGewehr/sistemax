using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Eventos de DOMÍNIO de SessaoCaixa — módulo-internos (IDomainEvent, SharedKernel), NÃO eventos de
// INTEGRAÇÃO (IIntegrationEvent, Modules.Abstractions). Ver EventosDoAgregado.cs para o mesmo
// racional já em uso por MovimentoFinanceiro/ContaFinanceiraBase: "a Application lê
// AggregateRoot.DomainEvents após persistir e decide o que fazer com eles".
//
// DECISÃO DE DESIGN — por que estes eventos NÃO viram IIntegrationEvent nesta revisão (o pedido
// original cogitava "fechamento alimenta MovimentoFinanceiro/saldo da ContaBancariaCaixa"):
// MovimentoFinanceiro.Registrar EXIGE ParcelaId não-vazio ("todo MovimentoFinanceiro precisa
// referenciar uma Parcela de origem" — ver MovimentoFinanceiro.cs). Suprimento/sangria do ritual
// de caixa físico não são parcelas de contas a pagar/receber — forçar uma Parcela fake só para
// satisfazer o invariante corromperia o ledger de competência (docs/financeiro-datamodel.md §3/§4)
// sem nenhum ganho real. Até o dia em que MovimentoFinanceiro evoluir para aceitar um ParcelaId
// OPCIONAL (ou nascer um segundo agregado de "caixa físico" com o próprio ledger — decisão de
// arquitetura fora do escopo desta tarefa), CaixaFechado fica só como fato de domínio: quem
// precisar do "saldo físico da gaveta" consulta SessaoCaixa diretamente
// (GET /financeiro/caixa/atual|historico), não MovimentoFinanceiro/ContasBancariasService. Mesma
// disciplina do R5 do CLAUDE.md irmão: "comece como documentação; promova quando 2+ subscribers
// existirem" — hoje não existe nenhum subscriber cross-módulo para estes fatos.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Uma sessão de caixa físico foi aberta — gaveta liberada para o turno.</summary>
public sealed record CaixaAberto(
    string SessaoId, string BusinessId, string ContaCaixaId, string OperadorId, long SaldoAberturaCentavos) : DomainEvent;

/// <summary>Suprimento (reforço de troco) registrado numa sessão aberta.</summary>
public sealed record SuprimentoRegistrado(
    string SessaoId, string BusinessId, string MovimentoId, long ValorCentavos, string Motivo) : DomainEvent;

/// <summary>Sangria (retirada) registrada numa sessão aberta — nunca excede o saldo esperado no
/// momento do registro (ver <see cref="SessaoCaixa.RegistrarSangria"/>).</summary>
public sealed record SangriaRegistrada(
    string SessaoId, string BusinessId, string MovimentoId, long ValorCentavos, string Motivo) : DomainEvent;

/// <summary>Venda em espécie registrada numa sessão aberta.</summary>
public sealed record VendaEmEspecieRegistrada(
    string SessaoId, string BusinessId, string MovimentoId, long ValorCentavos) : DomainEvent;

/// <summary>
/// Sessão fechada com contagem física — carrega a DIFERENÇA já calculada (contado − esperado),
/// positiva = sobra, negativa = falta. É o fato que o Super Consultor da tela "Fluxo de Caixa"
/// consome (ConsultorInsight: "as faltas do mês somam R$X... se concentram nas quintas à tarde").
/// </summary>
public sealed record CaixaFechado(
    string SessaoId, string BusinessId, string ContaCaixaId, string OperadorId,
    long SaldoEsperadoCentavos, long SaldoInformadoCentavos, long DiferencaCentavos) : DomainEvent;
