using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.CashDrawer;

public sealed class NullCashDrawerAdapter : ICashDrawerAdapter
{
    private static readonly Error NaoConfigurada = new("hardware.gaveta.nao_configurada", "Nenhuma gaveta configurada para este terminal.");

    public Task<Result> OpenAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar(NaoConfigurada));

    public Task<Result<DrawerState>> TryGetStateAsync(CancellationToken ct = default) => Task.FromResult(Result.Falhar<DrawerState>(NaoConfigurada));
}
