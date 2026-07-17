using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Hardware.Devices.Printer;
using SistemaX.Infrastructure.Hardware.Manager;

namespace SistemaX.Infrastructure.Hardware.PrintQueue;

/// <summary>
/// Drena a fila de impressão persistida — o MESMO princípio de outbox durável usado para dados
/// de negócio, reaplicado a hardware (ver docs/robustez §5). Dispara em dois gatilhos:
/// (1) timer periódico (<see cref="HardwareOptions.PrintQueueRetryInterval"/>), e
/// (2) imediatamente quando <see cref="HardwareManager"/> reporta que a impressora reconectou —
/// nunca espera até 10s pra tentar de novo um cupom que já pode imprimir agora.
/// </summary>
public sealed class PrintQueueProcessor(
    IPrintQueueStore store,
    HardwareManager hardwareManager,
    IOptions<HardwareOptions> options,
    ILogger<PrintQueueProcessor> logger) : BackgroundService
{
    private readonly SemaphoreSlim _drainGate = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        hardwareManager.DeviceReconnected += OnDeviceReconnected;
        try
        {
            using var timer = new PeriodicTimer(options.Value.PrintQueueRetryInterval);
            do
            {
                await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        finally
        {
            hardwareManager.DeviceReconnected -= OnDeviceReconnected;
        }
    }

    private void OnDeviceReconnected(string deviceKey)
    {
        if (deviceKey != "Printer")
        {
            return;
        }

        // Fire-and-forget deliberado: o próprio DrainOnceAsync é serializado pelo gate, e
        // qualquer exceção é tratada internamente (nunca deve escapar de um handler de evento).
        _ = DrainOnceAsync(CancellationToken.None);
    }

    private async Task DrainOnceAsync(CancellationToken ct)
    {
        if (!await _drainGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return; // já há uma drenagem em andamento (disparada pelo timer ou pelo evento) — não roda em paralelo
        }

        try
        {
            var pending = await store.GetPendingAsync(maxItems: 50, ct).ConfigureAwait(false);
            foreach (var job in pending)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                IReadOnlyList<PrintCommand>? commands;
                try
                {
                    commands = JsonSerializer.Deserialize<List<PrintCommand>>(job.CommandsJson);
                }
                catch (JsonException ex)
                {
                    logger.LogError(ex, "Job de impressão {Id} tem JSON inválido — marcando como falho definitivamente (não é um problema de hardware, é dado corrompido).", job.Id);
                    await store.MarkFailedAsync(job.Id, $"JSON inválido: {ex.Message}", ct).ConfigureAwait(false);
                    continue;
                }

                if (commands is null or [])
                {
                    await store.MarkFailedAsync(job.Id, "Job sem comandos após desserialização.", ct).ConfigureAwait(false);
                    continue;
                }

                var result = await hardwareManager.SafePrintAsync(commands, ct).ConfigureAwait(false);
                if (result.Sucesso)
                {
                    await store.MarkCompletedAsync(job.Id, ct).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Job de impressão {Id} falhou (tentativa {Tentativa}/{Max}): {Erro}.", job.Id, job.Attempts + 1, job.MaxAttempts, result.Erro.Mensagem);
                    await store.MarkFailedAsync(job.Id, result.Erro.Mensagem, ct).ConfigureAwait(false);

                    // Impressora ainda não voltou de verdade — não adianta continuar tentando os
                    // próximos jobs do lote agora; o próximo tick (ou o próximo DeviceReconnected) tenta de novo.
                    break;
                }
            }
        }
        finally
        {
            _drainGate.Release();
        }
    }
}
