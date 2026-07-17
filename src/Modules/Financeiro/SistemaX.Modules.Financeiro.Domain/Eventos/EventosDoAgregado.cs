using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Eventos;

// Eventos de DOMÍNIO (internos ao processo — não confundir com IIntegrationEvent do kernel de
// módulos). A camada de aplicação lê AggregateRoot.DomainEvents após persistir e decide o que
// fazer com eles (ex.: publicar ParcelaVencida como evento de integração para outros módulos).

public sealed record ContaCriada(string ContaId, string BusinessId, string TipoConta, long ValorTotalCentavos, string OrigemChave) : DomainEvent;

public sealed record ParcelaLiquidada(string ContaId, string ParcelaId, long ValorPagoCentavos, DateTimeOffset DataPagamento) : DomainEvent;

public sealed record ContaCancelada(string ContaId, string Motivo) : DomainEvent;

public sealed record ParcelaMarcadaVencida(string ContaId, string ParcelaId, long ValorCentavos, bool EhAPagar) : DomainEvent;

public sealed record MovimentoFinanceiroRegistrado(string MovimentoId, string BusinessId, long ValorCentavos, bool EhEntrada) : DomainEvent;

public sealed record MovimentoFinanceiroEstornado(string MovimentoId, string MovimentoOriginalId, long ValorCentavos) : DomainEvent;

public sealed record LancamentoContabilRegistrado(string LancamentoId, string BusinessId, string OrigemChave, long TotalCentavos) : DomainEvent;
