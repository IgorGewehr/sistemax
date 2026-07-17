using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Printer;

/// <summary>
/// Regra do módulo inteiro de hardware (ver docs/robustez §5): erros NUNCA são lançados ao
/// chamador — são reportados via <see cref="Health"/>/<see cref="Result"/>. O PDV deve continuar
/// operando mesmo se toda a impressora estiver offline.
/// </summary>
public interface IPrinterAdapter
{
    DeviceHealth Health { get; }

    Task<Result> ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Imprime a lista de comandos. Nunca lança — falha de hardware vira <see cref="Result.Falhar(Error)"/>.</summary>
    Task<Result> PrintAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default);
}
