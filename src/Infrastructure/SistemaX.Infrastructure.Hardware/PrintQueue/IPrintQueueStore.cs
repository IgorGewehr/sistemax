using SistemaX.Infrastructure.Hardware.Devices.Printer;

namespace SistemaX.Infrastructure.Hardware.PrintQueue;

/// <summary>
/// Persistência da fila de impressão — desacoplada de <c>SistemaX.Infrastructure.Local</c> de
/// propósito (ver README, seção "decisões de partição"): Hardware não referencia Local, então
/// mantém seu PRÓPRIO arquivo SQLite pequeno e independente, exatamente como faria um módulo de
/// negócio externo consumindo só pacotes, nunca a classe interna <c>LocalDatabase</c>.
/// </summary>
public interface IPrintQueueStore
{
    Task<string> EnqueueAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default);

    Task<IReadOnlyList<PrintJob>> GetPendingAsync(int maxItems, CancellationToken ct = default);

    Task MarkCompletedAsync(string id, CancellationToken ct = default);

    Task MarkFailedAsync(string id, string error, CancellationToken ct = default);

    Task<int> CountPendingAsync(CancellationToken ct = default);
}
