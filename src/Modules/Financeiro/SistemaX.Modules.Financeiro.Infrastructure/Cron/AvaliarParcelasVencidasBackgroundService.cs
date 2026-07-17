using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;

namespace SistemaX.Modules.Financeiro.Infrastructure.Cron;

/// <summary>
/// O "cron financeiro" de verdade (docs/financeiro-datamodel.md §4.2) — roda
/// <see cref="AvaliarParcelasVencidasUseCase"/> PERIODICAMENTE, sem gatilho manual, fechando o gap
/// que deixava o read-model de inadimplência/Radar do Simples parado até alguém chamar um endpoint
/// à mão. Mesmo molde de <c>ProjectionCatchUpHostedService</c> (Infrastructure.Local): catch-up
/// IMEDIATO no boot (parcelas que venceram enquanto o processo estava fora do ar não esperam o
/// primeiro intervalo) + loop com <see cref="TimeSpan"/> configurável + FAIL-OPEN (uma exceção num
/// ciclo é logada e nunca derruba o host) + cancelamento gracioso.
///
/// IDEMPOTENTE por reuso: <see cref="AvaliarParcelasVencidasUseCase"/> já reavalia o estado ATUAL a
/// cada chamada (nunca reafirma um evento sobre uma parcela que já está <c>Atrasado</c>) — rodar
/// este ciclo 2x seguidas, ou reiniciar o processo no meio de um ciclo, nunca duplica
/// <c>ParcelaVencida</c> publicado.
///
/// Um Singleton (<c>BackgroundService</c>) resolvendo um caso de uso Scoped precisa de escopo
/// PRÓPRIO por rodada — mesmo racional de <c>ProjectionRunner.ExecutarTudoAsync</c>: resolver
/// direto no construtor deste Singleton seria captive dependency.
/// </summary>
public sealed class AvaliarParcelasVencidasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITenantsDeInstalacao tenants,
    IOptions<FinanceiroCronOptions> options,
    ILogger<AvaliarParcelasVencidasBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = options.Value.IntervaloAvaliacaoParcelasVencidas;

        // Primeira rodada IMEDIATA — mesmo comportamento de ProjectionCatchUpHostedService: não
        // espera o primeiro intervalo pra marcar parcelas que já venceram durante o boot.
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
            var businessIds = await tenants.ObterBusinessIdsAsync(ct).ConfigureAwait(false);
            foreach (var businessId in businessIds)
            {
                await using var escopo = scopeFactory.CreateAsyncScope();
                var useCase = escopo.ServiceProvider.GetRequiredService<AvaliarParcelasVencidasUseCase>();
                await useCase.ExecutarAsync(businessId, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // encerramento normal do host — não é falha de avaliação.
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao avaliar parcelas vencidas — reavalia no próximo ciclo ({Intervalo}).",
                options.Value.IntervaloAvaliacaoParcelasVencidas);
        }
    }
}
