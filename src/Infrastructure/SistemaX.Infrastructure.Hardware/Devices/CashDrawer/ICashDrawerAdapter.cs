using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.CashDrawer;

/// <summary>
/// Nem todo "dispositivo" no diagrama de hardware é uma conexão física separada: a gaveta de
/// dinheiro tipicamente é acionada por um comando ESC/POS através da MESMA conexão da impressora
/// (ver docs/robustez §5 e <see cref="PrinterDrivenCashDrawerAdapter"/>) — esta interface existe
/// para que o resto do app peça "abra a gaveta" sem saber ou se importar com esse detalhe de topologia.
/// </summary>
public interface ICashDrawerAdapter
{
    Task<Result> OpenAsync(CancellationToken ct = default);

    /// <summary>
    /// Honesto sobre o nível de confiança (ver <see cref="DrawerState"/>): retorna
    /// <see cref="Result.Falhar(Error)"/> quando este hardware não tem como saber de verdade —
    /// nunca finge uma leitura de sensor que não existe.
    /// </summary>
    Task<Result<DrawerState>> TryGetStateAsync(CancellationToken ct = default);
}
