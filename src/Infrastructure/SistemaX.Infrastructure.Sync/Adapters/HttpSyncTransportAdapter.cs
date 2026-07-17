using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.Adapters;

/// <inheritdoc cref="ISyncTransportAdapter"/>
public sealed class HttpSyncTransportAdapter(HttpClient httpClient, IOptions<SyncOptions> options, ILogger<HttpSyncTransportAdapter> logger) : ISyncTransportAdapter
{
    public async Task<PushBatchResult> PushBatchAsync(IReadOnlyList<OutboxMessage> batch, string terminalId, CancellationToken ct = default)
    {
        var request = new SyncPushRequest(
            terminalId,
            batch.Select(m => new SyncPushRequestItem(m.Id, m.EntityType, m.EntityId, m.Operation.ToString(), m.PayloadJson, m.CreatedAtUtc.ToUnixTimeMilliseconds())).ToList());

        try
        {
            using var response = await httpClient.PostAsJsonAsync(options.Value.PushPath, request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Push ({Hop}) recebeu status {Status} do próximo salto — tratando como falha de transporte.", options.Value.HopName, (int)response.StatusCode);
                return new PushBatchResult(TransportOk: false, Items: Array.Empty<PushItemResult>());
            }

            var body = await response.Content.ReadFromJsonAsync<SyncPushResponse>(ct).ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Resposta de push vazia.");

            var items = body.Items
                .Select(i => new PushItemResult(i.Id, Enum.Parse<PushItemOutcome>(i.Outcome), i.Detail))
                .ToList();

            return new PushBatchResult(TransportOk: true, Items: items);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            // Timeout/DNS/rede — não sabemos se o servidor recebeu ou não. Não é um erro de
            // validação por item; é falha de TRANSPORTE. O lote inteiro fica pendente para a
            // próxima tentativa — reenviar depois é seguro por construção (idempotência por ULID).
            logger.LogWarning(ex, "Falha de transporte no push ({Hop}).", options.Value.HopName);
            return new PushBatchResult(TransportOk: false, Items: Array.Empty<PushItemResult>());
        }
    }

    public async Task<PullResult> PullAsync(SyncCursor cursor, string terminalId, int maxItems, CancellationToken ct = default)
    {
        var url = $"{options.Value.PullPath}?since={cursor.ServerSequence}&excludeTerminalId={Uri.EscapeDataString(terminalId)}&maxItems={maxItems}";

        try
        {
            var body = await httpClient.GetFromJsonAsync<SyncPullResponse>(url, ct).ConfigureAwait(false);
            if (body is null)
            {
                return new PullResult(TransportOk: false, Changes: Array.Empty<RemoteChange>(), NewServerSequence: cursor.ServerSequence);
            }

            var changes = body.Items
                .Select(i => new RemoteChange(i.Id, i.EntityType, i.EntityId, i.Operation, i.PayloadJson, i.OriginTerminalId, i.ServerSequence, DateTimeOffset.FromUnixTimeMilliseconds(i.OccurredAtUtcMs)))
                .ToList();

            return new PullResult(TransportOk: true, Changes: changes, NewServerSequence: body.NewServerSequence);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Falha de transporte no pull ({Hop}).", options.Value.HopName);
            return new PullResult(TransportOk: false, Changes: Array.Empty<RemoteChange>(), NewServerSequence: cursor.ServerSequence);
        }
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(options.Value.PingPath, ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
