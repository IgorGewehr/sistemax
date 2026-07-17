using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scanner;

/// <summary>
/// Estado padrão — também é o adapter CORRETO para o caso majoritário de scanner keyboard-wedge
/// (nenhum driver necessário; a leitura chega como teclado normal na UI). Só configure
/// <see cref="SerialBarcodeScannerAdapter"/> se o scanner físico realmente falar serial/RS-232 cru.
/// </summary>
public sealed class NullBarcodeScannerAdapter : IBarcodeScannerAdapter
{
    public DeviceHealth Health { get; } = DeviceHealth.NuncaConectado;

    public event Action<string>? OnScan { add { } remove { } }

    public Task<Result> ConnectAsync(CancellationToken ct = default) => Task.FromResult(Result.Ok());

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
}
