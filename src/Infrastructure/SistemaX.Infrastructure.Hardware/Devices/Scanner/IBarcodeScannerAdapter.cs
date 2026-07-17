using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scanner;

/// <summary>
/// A maioria dos leitores de código de barras de balcão é "keyboard-wedge" (HID): o SO os vê
/// como um teclado comum digitando o código seguido de Enter — não precisam de driver nenhum, e
/// a captura acontece na camada de UI (input focado), não aqui. Esta interface serve para os
/// MINORITÁRIOS que falam serial/RS-232 "cru" e precisam de um adapter de verdade (ver
/// docs/robustez §5 — mapeamento de topologia real em vez de "1 adapter = 1 dispositivo físico").
/// </summary>
public interface IBarcodeScannerAdapter
{
    DeviceHealth Health { get; }

    /// <summary>Disparado a cada leitura completa (após o terminador do protocolo, ex.: CR/LF).</summary>
    event Action<string>? OnScan;

    Task<Result> ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);
}
