using System.Collections.Concurrent;
using SistemaX.Modules.Abstractions;

namespace SistemaX.Modules.Compras.Tests.Fakes;

/// <summary>Captura os eventos publicados em memória — suficiente para testar quem PRODUZ eventos
/// (ConfirmarRecebimento/Estornar), inclusive a ORDEM de publicação.</summary>
public sealed class FakeIntegrationEventBus : IIntegrationEventBus
{
    private readonly ConcurrentQueue<IIntegrationEvent> _publicados = new();

    public IReadOnlyList<IIntegrationEvent> Publicados => _publicados.ToArray();

    public Task PublishAsync(IIntegrationEvent evento, CancellationToken ct = default)
    {
        _publicados.Enqueue(evento);
        return Task.CompletedTask;
    }
}
