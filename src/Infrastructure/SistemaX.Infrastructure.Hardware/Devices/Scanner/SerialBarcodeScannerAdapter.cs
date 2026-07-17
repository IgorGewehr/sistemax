using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scanner;

/// <summary>Scanner serial/RS-232 "cru" (minoria — a maioria dos scanners é keyboard-wedge, ver <see cref="NullBarcodeScannerAdapter"/>).</summary>
public sealed class SerialBarcodeScannerAdapter(string portName, int baudRate, ILogger<SerialBarcodeScannerAdapter> logger) : IBarcodeScannerAdapter, IDisposable
{
    private readonly StringBuilder _lineBuffer = new();
    private SerialPort? _serialPort;

    public DeviceHealth Health { get; private set; } = DeviceHealth.NuncaConectado;

    public event Action<string>? OnScan;

    public Task<Result> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var port = new SerialPort(portName, baudRate) { ReadTimeout = 500, WriteTimeout = 500 };
            port.DataReceived += OnDataReceived;
            port.Open();

            _serialPort?.Dispose();
            _serialPort = port;
            Health = new DeviceHealth(DeviceStatus.Connected, null, DateTimeOffset.UtcNow);
            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Health = new DeviceHealth(DeviceStatus.Error, ex.Message, Health.LastConnectedAtUtc);
            logger.LogWarning(ex, "Falha ao conectar no scanner serial {Porta}.", portName);
            return Task.FromResult(Result.Falhar(new Error("hardware.scanner.conexao_falhou", ex.Message)));
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_serialPort is not null)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.Dispose();
            _serialPort = null;
        }

        Health = Health with { Status = DeviceStatus.Disconnected };
        return Task.CompletedTask;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is null)
        {
            return;
        }

        try
        {
            var chunk = _serialPort.ReadExisting();
            foreach (var ch in chunk)
            {
                if (ch is '\r' or '\n')
                {
                    if (_lineBuffer.Length > 0)
                    {
                        OnScan?.Invoke(_lineBuffer.ToString());
                        _lineBuffer.Clear();
                    }
                }
                else
                {
                    _lineBuffer.Append(ch);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
        {
            logger.LogDebug(ex, "Erro transitório lendo scanner serial {Porta} — ignorando este ciclo.", portName);
        }
    }

    public void Dispose()
    {
        if (_serialPort is not null)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.Dispose();
        }
    }
}
