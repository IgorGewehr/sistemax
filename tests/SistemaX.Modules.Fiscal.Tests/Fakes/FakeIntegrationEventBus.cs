using System.Collections.Concurrent;
using SistemaX.Modules.Abstractions;

namespace SistemaX.Modules.Fiscal.Tests.Fakes;

/// <summary>Captura os eventos publicados em memória — mesmo papel do fake homônimo em
/// Estoque/Vendas/Compras/Financeiro.Tests.</summary>
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
