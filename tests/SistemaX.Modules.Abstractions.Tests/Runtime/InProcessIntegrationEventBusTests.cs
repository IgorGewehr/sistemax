using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;

namespace SistemaX.Modules.Abstractions.Tests.Runtime;

/// <summary>
/// F0 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/
/// ADR-0005) — persist-then-dispatch: o bus nunca pode despachar um evento aos assinantes sem
/// primeiro tê-lo persistido no ledger.
/// </summary>
public sealed class InProcessIntegrationEventBusTests
{
    private sealed record EventoDeTeste(string TenantId, string ChaveIdempotencia, DateTimeOffset OcorridoEm, string Dado)
        : IIntegrationEvent;

    private sealed class LedgerFake : IIntegrationEventLedgerStore
    {
        private readonly HashSet<string> _chavesJaVistas = [];
        public List<string> OrdemDeChamadas { get; } = [];
        public bool LancarNoAppend { get; set; }

        public Task<bool> AppendAsync(string tipo, string tenantId, string payloadJson, DateTimeOffset ocorridoEm, string chaveIdempotencia, CancellationToken ct = default)
        {
            OrdemDeChamadas.Add($"persist:{chaveIdempotencia}");

            if (LancarNoAppend)
                throw new InvalidOperationException("Falha simulada de persistência do ledger.");

            return Task.FromResult(_chavesJaVistas.Add(chaveIdempotencia));
        }

        public Task<IReadOnlyList<IntegrationEventLedgerEntry>> LerAPartirDoCursorAsync(long afterCursor, int maxBatchSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IntegrationEventLedgerEntry>>([]);

        public Task<long> ObterUltimoCursorAsync(CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class HandlerDeTeste(LedgerFake ledger) : IIntegrationEventHandler<EventoDeTeste>
    {
        public List<EventoDeTeste> Recebidos { get; } = [];

        public Task HandleAsync(EventoDeTeste evento, CancellationToken ct = default)
        {
            ledger.OrdemDeChamadas.Add($"dispatch:{evento.ChaveIdempotencia}");
            Recebidos.Add(evento);
            return Task.CompletedTask;
        }
    }

    private static (InProcessIntegrationEventBus Bus, LedgerFake Ledger, HandlerDeTeste Handler) MontarCenario()
    {
        var ledger = new LedgerFake();
        var handler = new HandlerDeTeste(ledger);

        var services = new ServiceCollection();
        services.AddSingleton<IIntegrationEventHandler<EventoDeTeste>>(handler);
        var provider = services.BuildServiceProvider();

        var bus = new InProcessIntegrationEventBus(provider.GetRequiredService<IServiceScopeFactory>(), ledger);
        return (bus, ledger, handler);
    }

    [Fact]
    public async Task PublishAsync_PersisteNoLedgerAntesDeDespachar()
    {
        var (bus, ledger, handler) = MontarCenario();
        var evento = new EventoDeTeste("tenant-1", "teste:1", DateTimeOffset.UtcNow, "dado-1");

        await bus.PublishAsync(evento);

        Assert.Equal(["persist:teste:1", "dispatch:teste:1"], ledger.OrdemDeChamadas);
        Assert.Single(handler.Recebidos);
    }

    [Fact]
    public async Task PublishAsync_FalhaAoPersistir_NuncaDespacha()
    {
        var (bus, ledger, handler) = MontarCenario();
        ledger.LancarNoAppend = true;
        var evento = new EventoDeTeste("tenant-1", "teste:2", DateTimeOffset.UtcNow, "dado-2");

        await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync(evento));

        Assert.Empty(handler.Recebidos); // nunca despachou — persistência falhou primeiro
    }

    [Fact]
    public async Task PublishAsync_MesmaChaveIdempotenciaDuasVezes_NaoImpedeDispatch()
    {
        // Idempotência é do LEDGER (não duplica a linha) — o dispatch aos handlers continua
        // acontecendo em toda chamada, exatamente como antes desta mudança (a idempotência de
        // ENTREGA continua sendo responsabilidade do handler, regra dura já documentada em
        // IIntegrationEventHandler).
        var (bus, ledger, handler) = MontarCenario();
        var evento = new EventoDeTeste("tenant-1", "teste:3", DateTimeOffset.UtcNow, "dado-3");

        await bus.PublishAsync(evento);
        await bus.PublishAsync(evento);

        Assert.Equal(2, handler.Recebidos.Count);
    }
}
