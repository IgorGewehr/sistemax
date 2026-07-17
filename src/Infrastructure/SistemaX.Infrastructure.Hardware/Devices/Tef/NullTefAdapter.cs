using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

/// <summary>Estado padrão até o operador configurar um provedor de TEF de verdade.</summary>
public sealed class NullTefAdapter(string provider = "none") : ITefAdapter
{
    private static readonly Error NaoConfigurado = new("hardware.tef.nao_configurado", "Nenhum provedor de TEF configurado para este terminal.");

    public string Provider { get; } = provider;

    public DeviceHealth Health { get; } = DeviceHealth.NuncaConectado;

    public Task<Result> ConnectAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar(NaoConfigurado));

    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<Result<TefTransactionResult>> StartTransactionAsync(TefTransactionRequest request, CancellationToken ct = default)
        => Task.FromResult(Result.Falhar<TefTransactionResult>(NaoConfigurado));

    public Task<Result<TefStatusConsultaResult>> GetTransactionStatusAsync(string idempotencyKey, CancellationToken ct = default)
        => Task.FromResult(Result.Falhar<TefStatusConsultaResult>(NaoConfigurado));
}
