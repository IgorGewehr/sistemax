using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Sync.Client;

namespace SistemaX.Infrastructure.Sync.Realtime;

/// <summary>
/// Cliente WebSocket com heartbeat para o salto configurado. Papel duplo:
/// <list type="bullet">
/// <item>Recebe notificação em tempo real de que algo mudou no próximo salto (sem precisar
/// esperar o próximo <see cref="SyncOptions.FlushInterval"/>) — dispara um
/// <see cref="SyncEngine.FlushOnceAsync"/> imediato. O CONTEÚDO da mensagem não precisa ser
/// decodificado: o pull subsequente já traz a mudança completa via cursor — o WS é só o "toque
/// de campainha", nunca a fonte de verdade dos dados.</item>
/// <item>Ao (re)conectar, SEMPRE dispara um flush antes de confiar no fluxo ao vivo — nunca
/// assume que o WS entregou tudo enquanto esteve desconectado (a lição preservada do
/// Supermarket-OS, docs/robustez §4: "catch-up pull" ao reconectar).</item>
/// </list>
/// Reconecta com backoff exponencial e nunca lança para fora — cair o WS não pode derrubar o
/// processo; o motor de sync via timer periódico continua funcionando de qualquer forma.
/// </summary>
public sealed class SyncWebSocketClient(SyncEngine syncEngine, IOptions<SyncOptions> options, ILogger<SyncWebSocketClient> logger) : BackgroundService
{
    private static readonly ReadOnlyMemory<byte> HeartbeatPingPayload = "{\"type\":\"ping\"}"u8.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectAttempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunConnectionLifecycleAsync(stoppingToken).ConfigureAwait(false);
                reconnectAttempt = 0; // sessão terminou normalmente (ex.: servidor fechou) — reseta o backoff
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                reconnectAttempt++;
                logger.LogWarning(ex, "Conexão WS ({Hop}) caiu — tentativa de reconexão #{Tentativa}.", options.Value.HopName, reconnectAttempt);
            }

            var delay = BackoffCalculator.Calculate(reconnectAttempt, options.Value.BackoffBase, options.Value.BackoffMax);
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task RunConnectionLifecycleAsync(CancellationToken ct)
    {
        using var socket = new ClientWebSocket();
        var uri = BuildWebSocketUri(options.Value);

        await socket.ConnectAsync(uri, ct).ConfigureAwait(false);
        logger.LogInformation("WS conectado ({Hop}) em {Uri}.", options.Value.HopName, uri);

        // Catch-up pull: nunca confiar que o WS vai entregar, sozinho, tudo que mudou enquanto
        // este cliente esteve desconectado.
        await syncEngine.FlushOnceAsync(ct).ConfigureAwait(false);

        using var heartbeatTimer = new PeriodicTimer(options.Value.HeartbeatInterval);
        var receiveTask = ReceiveLoopAsync(socket, ct);

        try
        {
            while (socket.State == WebSocketState.Open && await heartbeatTimer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (socket.State != WebSocketState.Open)
                {
                    break;
                }

                await socket.SendAsync(HeartbeatPingPayload, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            await receiveTask.ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];

        try
        {
            while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // Qualquer mensagem recebida = sinal de "algo mudou no próximo salto". O flush
                // seguinte traz a mudança de verdade via pull/cursor — não decodificamos o payload aqui.
                await syncEngine.FlushOnceAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Encerramento normal do host.
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "Loop de recepção WS ({Hop}) encerrado por exceção de transporte.", options.Value.HopName);
        }
    }

    private static Uri BuildWebSocketUri(SyncOptions opts)
    {
        var scheme = string.Equals(opts.UpstreamBaseAddress.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var builder = new UriBuilder(opts.UpstreamBaseAddress)
        {
            Scheme = scheme,
            Path = opts.WebSocketPath
        };
        return builder.Uri;
    }
}
