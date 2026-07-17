using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scale;

/// <summary>Estado padrão até o operador configurar uma balança de verdade.</summary>
public sealed class NullScaleAdapter : IScaleAdapter
{
    private static readonly Error NaoConfigurada = new("hardware.balanca.nao_configurada", "Nenhuma balança configurada para este terminal.");

    public DeviceHealth Health { get; } = DeviceHealth.NuncaConectado;

    public Task<Result> ConnectAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar(NaoConfigurada));

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<Result<Reading>> GetWeightAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar<Reading>(NaoConfigurada));
}
