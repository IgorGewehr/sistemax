namespace SistemaX.Infrastructure.Hardware.Common;

/// <summary>Estado de conexão de um dispositivo físico. Todo adapter reporta o seu — nunca lança para indicar "desconectado".</summary>
public enum DeviceStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>Snapshot de saúde de um dispositivo, consultado pelo <see cref="Manager.HardwareManager"/> no health-check periódico.</summary>
public sealed record DeviceHealth(DeviceStatus Status, string? LastError, DateTimeOffset? LastConnectedAtUtc)
{
    public static readonly DeviceHealth NuncaConectado = new(DeviceStatus.Disconnected, null, null);
}
