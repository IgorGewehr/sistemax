using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;

namespace SistemaX.Modules.Fiscal.Infrastructure.Cron;

/// <summary>
/// O job periódico que docs/fiscal/emissao-mapping.md §7.2/§9 nomeava como necessário — reprocessa
/// documentos fiscais presos em <c>NumeroAlocado</c> por falha transiente de infraestrutura,
/// SEM gatilho manual. Mesmo molde de <c>ProjectionCatchUpHostedService</c>/
/// <c>AvaliarParcelasVencidasBackgroundService</c>: catch-up imediato no boot + loop com intervalo
/// configurável + FAIL-OPEN (uma exceção num ciclo é logada e nunca derruba o host) + cancelamento
/// gracioso.
///
/// IDEMPOTENTE/FSM-SAFE por reuso do caso de uso: <see cref="RetransmitirDocumentosPendentesUseCase"/>
/// só toca documentos <c>NumeroAlocado</c> (nunca autorizado/cancelado/denegado — a query do
/// repositório já filtra por status) e deduplica por natureza (reavalia o estado ATUAL a cada
/// rodada; um documento que já autorizou simplesmente sai da lista de pendentes na próxima
/// consulta). BACKOFF via <c>AntiguidadeMinimaParaRetentar</c> (não martela um número recém-alocado)
/// e LIMITE DE TENTATIVAS via <c>IdadeMaximaAntesDeDesistir</c> (desiste formalmente do número em
/// vez de retentar pra sempre) — ambos configuráveis em <see cref="FiscalCronOptions"/>.
/// </summary>
public sealed class RetransmissaoFiscalBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITenantsDeInstalacao tenants,
    IOptions<FiscalCronOptions> options,
    ILogger<RetransmissaoFiscalBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = options.Value.IntervaloRetransmissao;

        await ExecutarUmCicloFailOpenAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalo, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // encerramento normal do host
            }

            await ExecutarUmCicloFailOpenAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ExecutarUmCicloFailOpenAsync(CancellationToken ct)
    {
        try
        {
            var opcoes = options.Value;
            var businessIds = await tenants.ObterBusinessIdsAsync(ct).ConfigureAwait(false);
            foreach (var businessId in businessIds)
            {
                await using var escopo = scopeFactory.CreateAsyncScope();
                var useCase = escopo.ServiceProvider.GetRequiredService<RetransmitirDocumentosPendentesUseCase>();
                await useCase.ExecutarAsync(businessId, opcoes.AntiguidadeMinimaParaRetentar, opcoes.IdadeMaximaAntesDeDesistir, ct)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // encerramento normal do host — não é falha de retransmissão.
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao retransmitir documentos fiscais pendentes — retenta no próximo ciclo ({Intervalo}).",
                options.Value.IntervaloRetransmissao);
        }
    }
}
