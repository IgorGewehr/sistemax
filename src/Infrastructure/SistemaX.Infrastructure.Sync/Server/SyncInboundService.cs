using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Sync.Adapters;
using SistemaX.Infrastructure.Sync.ChangeLog;
using SistemaX.Infrastructure.Sync.Conflict;
using SistemaX.Infrastructure.Sync.Idempotency;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Server;

/// <summary>
/// A lógica do lado RECEPTOR de um salto — onde o push/pull do salto anterior se conecta.
/// Framework-agnóstico de propósito: não é um controller HTTP, é o SERVIÇO que um controller/
/// minimal API do host chama. Isso mantém a fiação de rotas fora da partição de Infrastructure:
/// <list type="bullet">
/// <item><b>Salto 1</b> (recebendo de PDVs): <c>SistemaX.Store.Server</c> expõe
/// <c>POST /api/sync/batch</c> e <c>GET /api/sync/pull</c>, cada um desserializando o body/query
/// e chamando <see cref="ApplyBatchAsync"/>/<see cref="BuildPullResponseAsync"/> diretamente.</item>
/// <item><b>Salto 2</b> (recebendo do(s) ServidorDeLoja): <c>SistemaX.Cloud.Api</c> expõe as
/// MESMAS duas rotas sobre o MESMO contrato de wire (<see cref="SyncPushRequest"/>/
/// <see cref="SyncPullResponse"/>) — pode reusar esta classe (se rodar .NET) ou reimplementar o
/// mesmo contrato sobre Postgres; o formato da mensagem é o que garante compatibilidade, não a
/// linguagem/storage por trás.</item>
/// </list>
/// </summary>
public sealed class SyncInboundService(
    IProcessedMessageStore processedMessageStore,
    IChangeLogStore changeLogStore,
    IEnumerable<IRemoteChangeApplier> appliers,
    ConflictResolver conflictResolver,
    ILogger<SyncInboundService> logger)
{
    public async Task<SyncPushResponse> ApplyBatchAsync(SyncPushRequest request, CancellationToken ct = default)
    {
        var results = new List<SyncPushResponseItem>(request.Items.Count);

        foreach (var item in request.Items)
        {
            if (await processedMessageStore.WasProcessedAsync(item.Id, ct).ConfigureAwait(false))
            {
                // Idempotência ponta a ponta: o terminal reenviou um lote (total ou parcialmente)
                // já aplicado numa tentativa anterior cujo ACK se perdeu por falha de rede. Responder
                // AlreadySynced sem reaplicar é o que torna o reenvio seguro por construção.
                results.Add(new SyncPushResponseItem(item.Id, nameof(PushItemOutcome.AlreadySynced), null));
                continue;
            }

            try
            {
                var strategy = conflictResolver.StrategyFor(item.EntityType);
                var applier = appliers.FirstOrDefault(a => string.Equals(a.EntityType, item.EntityType, StringComparison.OrdinalIgnoreCase));
                var occurredAt = DateTimeOffset.FromUnixTimeMilliseconds(item.CreatedAtUtcMs);

                if (applier is not null)
                {
                    var change = new RemoteChange(item.Id, item.EntityType, item.EntityId, item.Operation, item.PayloadJson, request.TerminalId, ServerSequence: 0, occurredAt);
                    await applier.ApplyAsync(change, strategy, ct).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Nenhum IRemoteChangeApplier para '{EntityType}' — mudança {Id} só entra no changelog (para outros terminais puxarem), sem aplicação local nesta camada.", item.EntityType, item.Id);
                }

                await changeLogStore.AppendAsync(
                    new IncomingChange(item.Id, item.EntityType, item.EntityId, item.Operation, item.PayloadJson, request.TerminalId, occurredAt),
                    ct).ConfigureAwait(false);

                await processedMessageStore.MarkProcessedAsync(item.Id, item.EntityType, item.EntityId, ct).ConfigureAwait(false);

                results.Add(new SyncPushResponseItem(item.Id, nameof(PushItemOutcome.Confirmed), null));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao aplicar mudança {Id} ({EntityType}) do terminal {TerminalId}.", item.Id, item.EntityType, request.TerminalId);
                results.Add(new SyncPushResponseItem(item.Id, nameof(PushItemOutcome.Rejected), ex.Message));
            }
        }

        return new SyncPushResponse(results);
    }

    public async Task<SyncPullResponse> BuildPullResponseAsync(long sinceServerSequence, string excludeTerminalId, int maxItems, CancellationToken ct = default)
    {
        var changes = await changeLogStore.GetSinceAsync(sinceServerSequence, excludeTerminalId, maxItems, ct).ConfigureAwait(false);
        var newSequence = changes.Count > 0 ? changes[^1].ServerSequence : sinceServerSequence;

        var items = changes
            .Select(c => new SyncPullResponseItem(c.Id, c.EntityType, c.EntityId, c.Operation, c.PayloadJson, c.OriginTerminalId, c.ServerSequence, c.OccurredAtUtc.ToUnixTimeMilliseconds()))
            .ToList();

        return new SyncPullResponse(items, newSequence);
    }
}
