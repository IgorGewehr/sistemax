using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.CasosDeUso;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Infrastructure.Cron;

/// <summary>
/// P0-3 (docs/financeiro/revisao-domain-fit-cnpj.md): fecha o gap de a corrente de receita
/// recorrente inteira ficar PARADA em produção — antes deste job, <see cref="GerarCobrancasAssinaturasUseCase"/>
/// só era chamado pelo <c>DemoSeeder</c>, então nenhuma assinatura real virava
/// <c>ContaAReceber</c>/receita sozinha. Mesmo molde de <see cref="AvaliarParcelasVencidasBackgroundService"/>/
/// <c>RetransmissaoFiscalBackgroundService</c>: catch-up IMEDIATO no boot (assinaturas com ciclo
/// vencido enquanto o processo estava fora do ar não esperam o primeiro intervalo) + loop com
/// <see cref="TimeSpan"/> configurável + FAIL-OPEN (uma exceção num ciclo é logada e nunca derruba
/// o host) + cancelamento gracioso.
///
/// IDEMPOTENTE por reuso: <see cref="GerarCobrancasAssinaturasUseCase"/> já faz catch-up em loop
/// respeitando o <c>Ciclo</c> de cada assinatura (mensal cobra todo mês; trimestral a cada 3 meses;
/// anual 1×/ano — nunca o valor cheio todo mês para ciclo não-mensal) e é seguro contra reexecução:
/// rodar este ciclo 2× seguidas, ou reiniciar o processo no meio de um ciclo, nunca duplica
/// <c>ContaAReceber</c> (chave <c>assinatura:{id}:{yyyyMM}</c> — ver <c>Assinatura.GerarCobranca</c>).
///
/// Um Singleton (<c>BackgroundService</c>) resolvendo um caso de uso Scoped precisa de escopo
/// PRÓPRIO por rodada — mesmo racional dos outros crons do módulo.
/// </summary>
public sealed class FaturarAssinaturasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ITenantsDeInstalacao tenants,
    IOptions<FinanceiroCronOptions> options,
    ILogger<FaturarAssinaturasBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = options.Value.IntervaloFaturamentoAssinaturas;

        // Primeira rodada IMEDIATA — mesmo comportamento dos demais crons do módulo: não espera o
        // primeiro intervalo pra faturar assinaturas cujo ciclo já venceu durante o boot.
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
                var relogio = escopo.ServiceProvider.GetRequiredService<IRelogio>();
                var assinaturasUseCase = escopo.ServiceProvider.GetRequiredService<GerarCobrancasAssinaturasUseCase>();
                await assinaturasUseCase.ExecutarAsync(businessId, relogio.Agora(), ct).ConfigureAwait(false);

                // Recorrências genéricas (aluguel/salário/outras contas fixas) — mesmo gap do
                // P0-3 (só o DemoSeeder chamava GerarContasRecorrentesUseCase); faturar junto no
                // mesmo cron é o que o plano recomenda (docs/financeiro/revisao-domain-fit-cnpj.md
                // §4, Fatia 1) e usa exatamente o mesmo catch-up idempotente.
                var recorrentesUseCase = escopo.ServiceProvider.GetRequiredService<GerarContasRecorrentesUseCase>();
                await recorrentesUseCase.ExecutarAsync(businessId, relogio.Agora(), ct).ConfigureAwait(false);

                // P1-4 — dunning: assinaturas em graça (Inadimplente) há tempo demais viram churn.
                // Mesmo cron (recomendação da Fatia 7 do plano): um BackgroundService a menos, e o
                // relógio de graça nunca fica sem ser reavaliado por mais que o intervalo do cron.
                var dunningUseCase = escopo.ServiceProvider.GetRequiredService<AvaliarDunningAssinaturasUseCase>();
                await dunningUseCase.ExecutarAsync(businessId, relogio.Agora(), options.Value.DiasGracaInadimplenciaAssinatura, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // encerramento normal do host — não é falha de faturamento.
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao faturar assinaturas/recorrências — retenta no próximo ciclo ({Intervalo}).",
                options.Value.IntervaloFaturamentoAssinaturas);
        }
    }
}
