using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scale;

public interface IScaleAdapter
{
    DeviceHealth Health { get; }

    Task<Result> ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>Última leitura válida recebida. Nunca lança — hardware ausente/ruído vira <see cref="Result.Falhar(Error)"/>.</summary>
    Task<Result<Reading>> GetWeightAsync(CancellationToken ct = default);
}
