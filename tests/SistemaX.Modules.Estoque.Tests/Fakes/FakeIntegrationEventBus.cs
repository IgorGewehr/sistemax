using System.Collections.Concurrent;
using SistemaX.Modules.Abstractions;

namespace SistemaX.Modules.Estoque.Tests.Fakes;

/// <summary>Captura os eventos publicados em memória — suficiente para testar quem PRODUZ eventos
/// (alertas de mínimo, reserva descoberta, perda registrada).</summary>
public sealed class FakeIntegrationEventBus : IIntegrationEventBus
{
    private readonly ConcurrentQueue<IIntegrationEvent> _publicados = new();

    public IReadOnlyCollection<IIntegrationEvent> Publicados => _publicados.ToArray();

    public Task PublishAsync(IIntegrationEvent evento, CancellationToken ct = default)
    {
        _publicados.Enqueue(evento);
        return Task.CompletedTask;
    }
}
