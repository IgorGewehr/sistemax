using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local.Recovery;

namespace SistemaX.Infrastructure.Local;

/// <summary>
/// Corrige a fraqueza do Supermarket-OS de só checar corrupção no boot (ver docs/robustez §2,
/// fraqueza 1): roda <c>PRAGMA quick_check</c> periodicamente EM BACKGROUND, fora do caminho
/// quente de vendas, durante a operação do dia — um disco degradando ao longo do dia é pego
/// antes do próximo restart, não só nele.
/// </summary>
public sealed class PeriodicIntegrityCheckService(
    ICorruptionRecoveryService corruptionRecoveryService,
    IOptions<LocalDatabaseOptions> options,
    ILogger<PeriodicIntegrityCheckService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.QuickCheckInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                await corruptionRecoveryService.RunQuickCheckAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Encerramento normal do host.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no quick_check periódico — seguirá tentando no próximo ciclo.");
            }
        }
    }
}
