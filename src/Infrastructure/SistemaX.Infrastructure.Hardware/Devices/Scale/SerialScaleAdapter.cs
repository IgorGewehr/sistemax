using System.IO.Ports;
using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Scale;

/// <summary>
/// Balança serial (RS-232) — mapeamento direto do <c>serialport</c> npm do Supermarket-OS para
/// <see cref="SerialPort"/> do BCL (ver docs/robustez §5). O parser de protocolo é resolvido por
/// nome a partir de <see cref="ScaleProtocolParsers.Todos"/> — trocar de fabricante é configuração,
/// nunca mudança de código deste adapter.
/// </summary>
public sealed class SerialScaleAdapter : IScaleAdapter, IDisposable
{
    private readonly string _portName;
    private readonly int _baudRate;
    private readonly Func<byte[], Reading?> _parser;
    private readonly ILogger<SerialScaleAdapter> _logger;
    private readonly object _bufferLock = new();
    private readonly List<byte> _buffer = [];

    private SerialPort? _serialPort;
    private Reading? _lastReading;

    public SerialScaleAdapter(string portName, int baudRate, string protocolName, ILogger<SerialScaleAdapter> logger)
    {
        _portName = portName;
        _baudRate = baudRate;
        _logger = logger;

        if (!ScaleProtocolParsers.Todos.TryGetValue(protocolName, out var parser))
        {
            throw new ArgumentException($"Protocolo de balança '{protocolName}' desconhecido — cadastre em ScaleProtocolParsers.Todos.", nameof(protocolName));
        }

        _parser = parser;
    }

    public DeviceHealth Health { get; private set; } = DeviceHealth.NuncaConectado;

    public Task<Result> ConnectAsync(CancellationToken ct = default)
    {
        Health = Health with { Status = DeviceStatus.Connecting };

        try
        {
            var port = new SerialPort(_portName, _baudRate)
            {
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };
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
            _logger.LogWarning(ex, "Falha ao conectar na balança serial {Porta}.", _portName);
            return Task.FromResult(Result.Falhar(new Error("hardware.balanca.conexao_falhou", ex.Message)));
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

    public async Task<Result<Reading>> GetWeightAsync(CancellationToken ct = default)
    {
        if (_serialPort is null || !_serialPort.IsOpen)
        {
            return Result.Falhar<Reading>(new Error("hardware.balanca.desconectada", "Balança não conectada."));
        }

        // Dá uma pequena janela para um frame novo chegar; senão usa a última leitura recente.
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(1500);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_lastReading is { } fresh && DateTimeOffset.UtcNow - fresh.LidoEmUtc < TimeSpan.FromSeconds(2))
            {
                return Result.Ok(fresh);
            }

            await Task.Delay(100, ct).ConfigureAwait(false);
        }

        return Result.Falhar<Reading>(new Error("hardware.balanca.sem_leitura", "Nenhuma leitura válida recebida da balança dentro do tempo esperado."));
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is null)
        {
            return;
        }

        try
        {
            var bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead <= 0)
            {
                return;
            }

            var chunk = new byte[bytesToRead];
            _ = _serialPort.Read(chunk, 0, bytesToRead);

            lock (_bufferLock)
            {
                _buffer.AddRange(chunk);
                DrainFramesLocked();
            }
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Erro transitório lendo balança serial {Porta} — ignorando este ciclo.", _portName);
        }
    }

    /// <summary>Extrai quantos frames completos (STX...BCC) estiverem prontos no buffer, descartando ruído entre eles.</summary>
    private void DrainFramesLocked()
    {
        const byte stx = 0x02;
        const int frameLength = 11;

        while (true)
        {
            var startIndex = _buffer.IndexOf(stx);
            if (startIndex < 0)
            {
                _buffer.Clear();
                return;
            }

            if (startIndex > 0)
            {
                _buffer.RemoveRange(0, startIndex); // descarta ruído antes do STX
            }

            if (_buffer.Count < frameLength)
            {
                return; // frame ainda incompleto — espera mais bytes
            }

            var frame = _buffer.GetRange(0, frameLength).ToArray();
            _buffer.RemoveRange(0, frameLength);

            var reading = _parser(frame);
            if (reading is not null)
            {
                _lastReading = reading;
            }
            // Frame com checksum inválido é simplesmente descartado (ScaleProtocolParsers retorna
            // null) — nunca promovido a leitura válida.
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
