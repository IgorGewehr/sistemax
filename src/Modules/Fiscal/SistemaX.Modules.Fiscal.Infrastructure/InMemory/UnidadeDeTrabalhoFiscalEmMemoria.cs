using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Infrastructure.InMemory;

/// <summary>
/// No-op — usado por padrão em teste (sem contexto SQLite). Os adapters InMemory dos demais ports
/// (<c>InMemorySequenciaFiscalRepository</c>/<c>InMemoryDocumentoFiscalRepository</c>) já são
/// atômicos dentro do próprio processo (<c>ConcurrentDictionary</c>) e nada neles sobrevive a um
/// crash — não há transação real para coordenar entre os dois, então iniciar/confirmar/desfazer
/// aqui é só um ponto de extensão que a Application chama sem precisar saber qual persistência
/// está por trás.
/// </summary>
public sealed class UnidadeDeTrabalhoFiscalEmMemoria : IUnidadeDeTrabalhoFiscal
{
    public Task IniciarAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
}
