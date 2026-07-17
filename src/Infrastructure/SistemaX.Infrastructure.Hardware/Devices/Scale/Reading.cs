namespace SistemaX.Infrastructure.Hardware.Devices.Scale;

public sealed record Reading(decimal Kg, bool Estavel, DateTimeOffset LidoEmUtc);
