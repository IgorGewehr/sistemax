using System.Collections.Concurrent;
using SistemaX.Modules.Abstractions;

namespace SistemaX.Modules.Vendas.Tests.Fakes;

/// <summary>Captura os eventos publicados em memória — suficiente para testar quem PRODUZ eventos
/// de integração (<c>ConcluirVendaUseCase</c>/<c>EstornarVendaUseCase</c>) sem precisar de um
/// barramento real. Mesmo padrão do <c>FakeIntegrationEventBus</c> do Financeiro.Tests.</summary>
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
