using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaX.Modules.Abstractions.Runtime;

/// <summary>
/// Barramento in-process de eventos de integração — o encanamento do "tudo alimenta o financeiro".
/// Resolve, pelo tipo DINÂMICO do evento, todos os <see cref="IIntegrationEventHandler{TEvent}"/>
/// registrados e entrega a cada um, dentro de um escopo de DI próprio.
///
/// Vive em <c>Modules.Abstractions</c> (não em Host.Desktop) porque os 3 hosts
/// (Host.Desktop/Store.Server/Cloud.Api) compartilham o MESMO composition root de módulos — cada
/// um só precisa de <c>services.AddSingleton&lt;IIntegrationEventBus, InProcessIntegrationEventBus&gt;()</c>,
/// sem duplicar a classe.
///
/// PERSIST-THEN-DISPATCH (F0 do plano de inteligência do Financeiro — ver
/// docs/financeiro/inteligencia-arquitetura.md §3.1 e ADR-0005): antes de qualquer coisa, o
/// evento é gravado no ledger append-only <c>integration_events</c> via
/// <see cref="IIntegrationEventLedgerStore"/> — idempotente por <see cref="IIntegrationEvent.ChaveIdempotencia"/>,
/// nunca lança em duplicata. SÓ DEPOIS o bus despacha aos assinantes, exatamente como sempre fez.
/// Isso fecha o gap central diagnosticado no plano: até aqui, o evento era entregue e EVAPORAVA —
/// nenhuma persistência, nenhum replay possível, histórico perdido a cada dia. O dispatch em si
/// NÃO muda de comportamento: a idempotência de entrega continua sendo responsabilidade do
/// handler (reentregar o mesmo evento é no-op no consumidor) — persistir uma segunda vez o MESMO
/// fato (replay, retry) não impede o dispatch de rodar de novo, exatamente como antes desta
/// mudança.
///
/// Contrato (ver <see cref="IIntegrationEventBus"/>): publica-se SEMPRE após o commit da transação
/// de origem. Hoje a entrega aos handlers é síncrona e local; a evolução prevista (docs/arquitetura)
/// é propagar entre as 3 camadas (PDV↔loja↔nuvem) pelo motor de sync — ortogonal ao ledger acima,
/// que é sobre HISTÓRICO consultável, não sobre replicação entre terminais.
/// </summary>
public sealed class InProcessIntegrationEventBus(IServiceScopeFactory scopeFactory, IIntegrationEventLedgerStore ledger) : IIntegrationEventBus
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> _cache = new();

    public async Task PublishAsync(IIntegrationEvent evento, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var tipoEvento = evento.GetType();

        // PERSIST — nunca despachar sem persistir primeiro. Serializa pelo tipo CONCRETO (não
        // pela interface IIntegrationEvent) para capturar todos os campos do record, exatamente
        // como LocalUnitOfWork.EnqueueOutboxAsync já faz para o payload do outbox.
        await ledger.AppendAsync(
            tipo: tipoEvento.Name,
            tenantId: evento.TenantId,
            payloadJson: JsonSerializer.Serialize(evento, tipoEvento),
            ocorridoEm: evento.OcorridoEm,
            chaveIdempotencia: evento.ChaveIdempotencia,
            ct: ct).ConfigureAwait(false);

        // THEN DISPATCH — inalterado em relação ao comportamento anterior a este ADR.
        var (tipoHandler, handle) = _cache.GetOrAdd(tipoEvento, static tipoEvento =>
        {
            var tipoHandler = typeof(IIntegrationEventHandler<>).MakeGenericType(tipoEvento);
            return (tipoHandler, tipoHandler.GetMethod("HandleAsync")!);
        });

        // AsyncScope (não CreateScope + using síncrono): um handler pode resolver serviços Scoped
        // que só implementam IAsyncDisposable — ex.: a persistência SQLite puxa ILocalSessao
        // (LocalSessao é IAsyncDisposable-only). Dispor esse escopo de forma síncrona lançaria
        // "type only implements IAsyncDisposable". await using descarta corretamente.
        await using var escopo = scopeFactory.CreateAsyncScope();
        foreach (var handler in escopo.ServiceProvider.GetServices(tipoHandler))
        {
            if (handler is null) continue;
            await (Task)handle.Invoke(handler, [evento, ct])!;
        }
    }
}
