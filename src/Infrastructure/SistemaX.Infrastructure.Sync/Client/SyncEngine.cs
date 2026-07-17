using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local.Identity;
using SistemaX.Infrastructure.Sync.Adapters;
using SistemaX.Infrastructure.Sync.Conflict;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Client;

/// <summary>
/// O motor de sync CLIENTE de UM salto — flush do outbox (push em lote com backoff exponencial)
/// seguido de pull por cursor (com prevenção de eco por TerminalId). É a MESMA classe para os 2
/// saltos da topologia (PDV→Loja e Loja→Nuvem); o que muda entre eles é só a
/// <see cref="SyncOptions"/> injetada (endereço upstream, nome do salto) — ver
/// <see cref="DependencyInjection.ServiceCollectionExtensions.AddSistemaXSyncClient"/>.
///
/// <see cref="FlushOnceAsync"/> é público e chamado tanto pelo timer periódico
/// (<see cref="ExecuteAsync"/>) quanto pelo <see cref="Realtime.SyncWebSocketClient"/> ao
/// reconectar ou receber notificação — nunca duas implementações divergentes do mesmo ciclo.
/// </summary>
public sealed class SyncEngine(
    ISyncStorageAdapter storage,
    ISyncTransportAdapter transport,
    IEnumerable<IRemoteChangeApplier> appliers,
    ConflictResolver conflictResolver,
    ITerminalIdentity terminalIdentity,
    IOptions<SyncOptions> options,
    ILogger<SyncEngine> logger) : BackgroundService
{
    private readonly SemaphoreSlim _flushGate = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FlushOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ciclo de sync ({Hop}) falhou inesperadamente — seguirá tentando no próximo intervalo.", options.Value.HopName);
            }

            try
            {
                await Task.Delay(options.Value.FlushInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Um ciclo completo: push do que está pendente, depois pull do que mudou nos outros
    /// terminais. Serializado por um gate — se o WS e o timer dispararem quase juntos, um espera
    /// o outro em vez de rodar dois flushes concorrentes.
    /// </summary>
    public async Task FlushOnceAsync(CancellationToken ct = default)
    {
        await _flushGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PushAsync(ct).ConfigureAwait(false);
            await PullAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _flushGate.Release();
        }
    }

    private async Task PushAsync(CancellationToken ct)
    {
        var pendingCount = await storage.CountPendingAsync(ct).ConfigureAwait(false);
        if (pendingCount >= options.Value.PendingQueueAlertThreshold)
        {
            // Fraqueza corrigida do Supermarket-OS (docs/robustez §3): fila sem alerta cresce
            // sem sinalização proativa. Nunca bloqueia a venda — só alerta, alto e claro.
            logger.LogCritical(
                "Fila de sync ({Hop}) com {Count} mensagens pendentes — acima do limite de alerta configurado ({Limite}). Terminal continua vendendo normalmente, mas o sync está atrasado ou o próximo salto está inacessível há muito tempo.",
                options.Value.HopName, pendingCount, options.Value.PendingQueueAlertThreshold);
        }

        var batch = await storage.GetPendingBatchAsync(options.Value.BatchSize, ct).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            return;
        }

        var terminalId = await terminalIdentity.GetTerminalIdAsync(ct).ConfigureAwait(false);
        var result = await transport.PushBatchAsync(batch, terminalId, ct).ConfigureAwait(false);

        if (!result.TransportOk)
        {
            // Falha de TRANSPORTE (rede/timeout) — o lote inteiro fica Pending, sem consumir
            // tentativa individual por item; o próximo ciclo tenta de novo. Isto é diferente de
            // "servidor respondeu e rejeitou o item" (tratado abaixo).
            logger.LogWarning("Push ({Hop}) falhou por transporte — {Count} itens seguem pendentes.", options.Value.HopName, batch.Count);
            return;
        }

        var byId = batch.ToDictionary(m => m.Id);
        foreach (var item in result.Items)
        {
            switch (item.Outcome)
            {
                case PushItemOutcome.Confirmed:
                case PushItemOutcome.AlreadySynced:
                    await storage.MarkConfirmedAsync([item.Id], ct).ConfigureAwait(false);
                    break;

                case PushItemOutcome.Rejected:
                    var attempts = byId.TryGetValue(item.Id, out var original) ? original.Attempts + 1 : 1;
                    if (attempts >= options.Value.MaxRetries)
                    {
                        logger.LogError(
                            "Mensagem {Id} ({EntityType}) excedeu {MaxRetries} tentativas — movendo para dead-letter. Motivo: {Detalhe}.",
                            item.Id, original?.EntityType, options.Value.MaxRetries, item.Detail);
                        await storage.MoveToDeadLetterAsync(item.Id, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        var delay = BackoffCalculator.Calculate(attempts, options.Value.BackoffBase, options.Value.BackoffMax);
                        await storage.MarkFailedAsync(item.Id, item.Detail ?? "Rejeitado pelo próximo salto.", delay, ct).ConfigureAwait(false);
                    }
                    break;
            }
        }
    }

    private async Task PullAsync(CancellationToken ct)
    {
        var terminalId = await terminalIdentity.GetTerminalIdAsync(ct).ConfigureAwait(false);
        var cursor = await storage.GetCursorAsync(ct).ConfigureAwait(false);

        var result = await transport.PullAsync(cursor, terminalId, options.Value.MaxPullItemsPerRequest, ct).ConfigureAwait(false);
        if (!result.TransportOk)
        {
            return;
        }

        foreach (var change in result.Changes)
        {
            var strategy = conflictResolver.StrategyFor(change.EntityType);
            var applier = appliers.FirstOrDefault(a => string.Equals(a.EntityType, change.EntityType, StringComparison.OrdinalIgnoreCase));

            if (applier is null)
            {
                logger.LogWarning(
                    "Nenhum IRemoteChangeApplier registrado para '{EntityType}' — mudança {Id} recebida do pull ({Hop}) foi ignorada localmente.",
                    change.EntityType, change.Id, options.Value.HopName);
                continue;
            }

            try
            {
                await applier.ApplyAsync(change, strategy, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Uma mudança que falha ao aplicar não pode travar o cursor: loga e segue —
                // senão o terminal fica preso pra sempre tentando reaplicar a mesma mudança.
                logger.LogError(ex, "Falha ao aplicar mudança {Id} ({EntityType}) recebida do pull ({Hop}).", change.Id, change.EntityType, options.Value.HopName);
            }
        }

        if (result.NewServerSequence != cursor.ServerSequence)
        {
            await storage.SaveCursorAsync(new SyncCursor(result.NewServerSequence), ct).ConfigureAwait(false);
        }
    }
}
