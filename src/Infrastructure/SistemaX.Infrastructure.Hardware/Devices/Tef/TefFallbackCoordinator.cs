using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

public enum TefAuthorizationOutcome
{
    Approved,
    Denied,

    /// <summary>
    /// Status permaneceu indeterminado mesmo após consultar o adquirente — NUNCA decida sozinho
    /// a partir daqui. Peça confirmação manual do operador/gerente antes de qualquer nova tentativa.
    /// </summary>
    RequiresManualConfirmation
}

public sealed record TefAuthorizationResult(TefAuthorizationOutcome Outcome, TefTransactionResult? Transacao, string? Motivo);

/// <summary>
/// A REGRA CRÍTICA deste projeto (ver docs/robustez/robustez-hardware-licoes.md §5, fraqueza 1):
/// NUNCA reenviar ou trocar de adquirente sem antes CONSULTAR o status da transação original. Um
/// timeout local não significa que o adquirente não processou a cobrança — é a receita clássica
/// para cobrança duplicada no cartão do cliente.
///
/// Fluxo: tenta o provedor primário com timeout. Se responder dentro do prazo, o resultado é
/// final (aprovado/negado). Se estourar o timeout — ambiguidade —, antes de qualquer nova ação
/// consulta <see cref="ITefAdapter.GetTransactionStatusAsync"/> NO MESMO provedor, pela MESMA
/// <see cref="TefTransactionRequest.IdempotencyKey"/>, algumas vezes com espera entre tentativas.
/// Só then decide: aprovada de verdade → usa esse resultado (nunca cobra de novo); negada de
/// verdade → seguro tentar o próximo provedor da lista; ainda indeterminado → EXIGE confirmação
/// manual, nunca decide sozinho.
///
/// NUNCA chame <see cref="ITefAdapter"/> diretamente do fluxo de venda — sempre por aqui.
/// </summary>
public sealed class TefFallbackCoordinator(IOptions<HardwareOptions> options, ILogger<TefFallbackCoordinator> logger)
{
    public async Task<TefAuthorizationResult> AuthorizeAsync(IReadOnlyList<ITefAdapter> providersEmOrdem, TefTransactionRequest request, CancellationToken ct = default)
    {
        if (providersEmOrdem.Count == 0)
        {
            return new TefAuthorizationResult(TefAuthorizationOutcome.RequiresManualConfirmation, null, "Nenhum provedor de TEF configurado.");
        }

        var primary = providersEmOrdem[0];
        var directResult = await TryStartWithTimeoutAsync(primary, request, ct).ConfigureAwait(false);

        if (directResult is not null)
        {
            // O provedor respondeu dentro do timeout — sem ambiguidade, resultado é final.
            return ToOutcome(directResult);
        }

        // AMBIGUIDADE: timeout (ou falha indeterminada) na tentativa direta. Antes de QUALQUER
        // nova ação, consulta o status real no MESMO adquirente pela MESMA chave.
        var status = await PollStatusUntilConclusiveAsync(primary, request.IdempotencyKey, ct).ConfigureAwait(false);

        switch (status.Status)
        {
            case TefTransactionStatus.Approved:
                logger.LogWarning(
                    "TEF {Provider}: timeout local na tentativa direta, mas a consulta de status confirma APROVADA (NSU {Nsu}) — usando esse resultado real, SEM nova cobrança.",
                    primary.Provider, status.Nsu);
                return new TefAuthorizationResult(
                    TefAuthorizationOutcome.Approved,
                    new TefTransactionResult(TefTransactionStatus.Approved, status.Nsu, null, null, status.MensagemAdquirente),
                    null);

            case TefTransactionStatus.Denied:
                logger.LogInformation(
                    "TEF {Provider}: timeout local, mas a consulta de status confirma NEGADA — seguro tentar o próximo provedor.",
                    primary.Provider);

                return providersEmOrdem.Count > 1
                    ? await AuthorizeAsync(providersEmOrdem.Skip(1).ToArray(), request, ct).ConfigureAwait(false)
                    : new TefAuthorizationResult(TefAuthorizationOutcome.Denied, null, status.MensagemAdquirente);

            default: // Pending ou Unknown mesmo após as tentativas de consulta
                logger.LogCritical(
                    "TEF {Provider}: status da transação (idempotencyKey={IdempotencyKey}) permanece INDETERMINADO após timeout e consulta ao adquirente — EXIGINDO confirmação manual do operador/gerente antes de qualquer nova tentativa de cobrança. NUNCA tentar automaticamente de novo neste estado.",
                    primary.Provider, request.IdempotencyKey);
                return new TefAuthorizationResult(
                    TefAuthorizationOutcome.RequiresManualConfirmation,
                    null,
                    "Status indeterminado no adquirente — peça ao operador/gerente que confirme manualmente (consultando o extrato do adquirente, se necessário) antes de tentar novamente.");
        }
    }

    private async Task<TefTransactionResult?> TryStartWithTimeoutAsync(ITefAdapter adapter, TefTransactionRequest request, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.Value.TefAuthorizationTimeout);

        try
        {
            var result = await adapter.StartTransactionAsync(request, timeoutCts.Token).ConfigureAwait(false);
            if (result.Falha)
            {
                // Falha de INFRA (ex.: adapter não conectado) — tratamos com a MESMA cautela de
                // um timeout: não sabemos com certeza que nada foi enviado ao adquirente.
                logger.LogWarning("TEF {Provider}: StartTransactionAsync falhou ({Erro}) — tratando como AMBÍGUO, nunca como 'com certeza não foi enviado'.", adapter.Provider, result.Erro.Mensagem);
                return null;
            }

            return result.Valor;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("TEF {Provider}: timeout de {Timeout} atingido na tentativa direta — status é DESCONHECIDO até consultar o adquirente.", adapter.Provider, options.Value.TefAuthorizationTimeout);
            return null;
        }
    }

    private async Task<TefStatusConsultaResult> PollStatusUntilConclusiveAsync(ITefAdapter adapter, string idempotencyKey, CancellationToken ct)
    {
        TefStatusConsultaResult? last = null;

        for (var attempt = 1; attempt <= options.Value.TefStatusPollMaxAttempts; attempt++)
        {
            var statusResult = await adapter.GetTransactionStatusAsync(idempotencyKey, ct).ConfigureAwait(false);
            if (statusResult.Sucesso)
            {
                last = statusResult.Valor;
                if (last.Status is TefTransactionStatus.Approved or TefTransactionStatus.Denied)
                {
                    return last;
                }
            }

            if (attempt < options.Value.TefStatusPollMaxAttempts)
            {
                await Task.Delay(options.Value.TefStatusPollInterval, ct).ConfigureAwait(false);
            }
        }

        return last ?? new TefStatusConsultaResult(TefTransactionStatus.Unknown, null, "Não foi possível consultar o status no adquirente.");
    }

    private static TefAuthorizationResult ToOutcome(TefTransactionResult result) => result.Status switch
    {
        TefTransactionStatus.Approved => new TefAuthorizationResult(TefAuthorizationOutcome.Approved, result, null),
        TefTransactionStatus.Denied => new TefAuthorizationResult(TefAuthorizationOutcome.Denied, result, result.MensagemAdquirente),
        _ => new TefAuthorizationResult(TefAuthorizationOutcome.RequiresManualConfirmation, result, "Status ambíguo retornado pelo adquirente na tentativa direta.")
    };
}
