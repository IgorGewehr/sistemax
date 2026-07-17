using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Printer;

/// <summary>
/// Impressora ESC/POS de rede (a maioria das térmicas de balcão no Brasil expõe uma porta TCP,
/// tipicamente 9100) — mapeamento direto do <c>node:net</c> do Supermarket-OS para
/// <see cref="TcpClient"/> (ver docs/robustez §5). Nunca lança para o chamador: toda exceção de
/// I/O vira <see cref="Result.Falhar(Error)"/> e atualiza <see cref="Health"/>.
/// </summary>
public sealed class TcpEscPosPrinterAdapter(string host, int port, ILogger<TcpEscPosPrinterAdapter> logger, int codePage = 860) : IPrinterAdapter
{
    private TcpClient? _client;

    public DeviceHealth Health { get; private set; } = DeviceHealth.NuncaConectado;

    public async Task<Result> ConnectAsync(CancellationToken ct = default)
    {
        Health = Health with { Status = DeviceStatus.Connecting };

        try
        {
            var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);

            _client?.Dispose();
            _client = client;
            Health = new DeviceHealth(DeviceStatus.Connected, null, DateTimeOffset.UtcNow);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or IOException)
        {
            Health = new DeviceHealth(DeviceStatus.Error, ex.Message, Health.LastConnectedAtUtc);
            logger.LogWarning(ex, "Falha ao conectar na impressora ESC/POS {Host}:{Port}.", host, port);
            return Result.Falhar(new Error("hardware.printer.conexao_falhou", ex.Message));
        }
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _client?.Dispose();
        _client = null;
        Health = Health with { Status = DeviceStatus.Disconnected };
        return Task.CompletedTask;
    }

    public async Task<Result> PrintAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default)
    {
        if (_client is null || !_client.Connected)
        {
            var connectResult = await ConnectAsync(ct).ConfigureAwait(false);
            if (connectResult.Falha)
            {
                return connectResult;
            }
        }

        try
        {
            var bytes = EscPosBuilder.Build(commands, codePage);
            var stream = _client!.GetStream();
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            Health = new DeviceHealth(DeviceStatus.Connected, null, DateTimeOffset.UtcNow);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
        {
            Health = new DeviceHealth(DeviceStatus.Error, ex.Message, Health.LastConnectedAtUtc);
            logger.LogWarning(ex, "Falha ao imprimir na impressora ESC/POS {Host}:{Port} — job vai para a fila persistida.", host, port);
            return Result.Falhar(new Error("hardware.printer.falha_impressao", ex.Message));
        }
    }
}
