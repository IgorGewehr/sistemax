using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Printer;

/// <summary>
/// Estado padrão até o operador configurar uma impressora de verdade — nunca lança, nunca
/// bloqueia o boot do terminal por falta de hardware físico conectado (ver docs/robustez §5).
/// </summary>
public sealed class NullPrinterAdapter : IPrinterAdapter
{
    private static readonly Error NaoConfigurado = new("hardware.printer.nao_configurado", "Nenhuma impressora configurada para este terminal.");

    public DeviceHealth Health { get; } = DeviceHealth.NuncaConectado;

    public Task<Result> ConnectAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar(NaoConfigurado));

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<Result> PrintAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default) => Task.FromResult(Result.Falhar(NaoConfigurado));
}
