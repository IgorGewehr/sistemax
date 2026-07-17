namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Assinante de um evento de integração. O Financeiro implementa vários destes
/// (VendaConcluida → recebível/receita, CompraRecebida → pagável/custo, …).
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent evento, CancellationToken ct = default);
}

/// <summary>
/// Barramento in-process de eventos de integração — o encanamento do "tudo alimenta o financeiro".
///
/// CONTRATO (regras duras):
///  • Publica-se SEMPRE após o commit da transação de origem, nunca no meio dela.
///  • A implementação (Infrastructure) resolve os <see cref="IIntegrationEventHandler{TEvent}"/>
///    via DI e garante ENTREGA AO MENOS UMA VEZ.
///  • IDEMPOTÊNCIA por <see cref="IIntegrationEvent.ChaveIdempotencia"/>: reprocessar o mesmo
///    evento é no-op no consumidor (não duplica lançamento financeiro).
/// </summary>
public interface IIntegrationEventBus
{
    Task PublishAsync(IIntegrationEvent evento, CancellationToken ct = default);
}
