using SistemaX.Infrastructure.Hardware.Devices.Printer;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.CashDrawer;

/// <summary>
/// A gaveta acionada pela MESMA conexão da impressora — não é um dispositivo próprio (ver
/// docs/robustez §5). Envia <see cref="PrintCommand.AbrirGaveta"/> através do
/// <see cref="IPrinterAdapter"/> já conectado, em vez de abrir uma segunda conexão física.
/// </summary>
public sealed class PrinterDrivenCashDrawerAdapter(IPrinterAdapter printerAdapter) : ICashDrawerAdapter
{
    private DrawerState _inferredState = DrawerState.Desconhecido;

    public async Task<Result> OpenAsync(CancellationToken ct = default)
    {
        var result = await printerAdapter.PrintAsync([new PrintCommand.AbrirGaveta()], ct).ConfigureAwait(false);
        if (result.Sucesso)
        {
            _inferredState = DrawerState.InferidoAberta;
        }

        return result;
    }

    public Task<Result<DrawerState>> TryGetStateAsync(CancellationToken ct = default)
    {
        // Honesto: a maioria das gavetas não tem sensor. Só sabemos "inferido aberta" a partir do
        // último comando enviado — nunca fingimos saber o estado físico real (ver DrawerState).
        return Task.FromResult(Result.Ok(_inferredState));
    }
}
