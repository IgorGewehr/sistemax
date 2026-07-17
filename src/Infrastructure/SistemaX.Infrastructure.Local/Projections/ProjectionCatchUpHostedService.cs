using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaX.Infrastructure.Local.Projections;

/// <summary>
/// Roda o catch-up de TODAS as projeções registradas — no boot E depois PERIODICAMENTE, a cada
/// <see cref="LocalDatabaseOptions.ProjectionCatchUpInterval"/> (registrado depois de
/// <c>LocalDatabaseBootstrapper</c> em <c>AddSistemaXLocalInfrastructure</c> — o Generic Host inicia
/// <see cref="IHostedService"/> em ordem de registro).
///
/// F1 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
/// §6/ADR-0005) fecha o gap documentado na F0: antes, o catch-up só rodava no boot — uma fact table
/// (<c>fato_receita_diaria</c>, <c>fato_margem_produto</c>, ...) ficava atrasada até o próximo
/// restart do processo, o que é inaceitável para análises quant que precisam de dado fresco (ex.:
/// previsão de caixa não pode olhar pra ontem se a venda de agora ainda não entrou na fact table).
///
/// FAIL-OPEN DELIBERADO (mantido da F0): uma falha num ciclo de catch-up nunca derruba o
/// <see cref="BackgroundService"/> nem o boot do app inteiro — só loga e tenta de novo no próximo
/// ciclo (mesma filosofia de <see cref="PeriodicIntegrityCheckService"/>, que já usa este molde de
/// loop com <c>try/catch</c> por iteração).
/// </summary>
public sealed class ProjectionCatchUpHostedService(
    ProjectionRunner runner,
    IOptions<LocalDatabaseOptions> options,
    ILogger<ProjectionCatchUpHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = options.Value.ProjectionCatchUpInterval;

        // Primeira rodada IMEDIATA (mesmo comportamento que a F0 já tinha no boot) — não espera o
        // primeiro intervalo pra dar o catch-up inicial.
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
            await runner.ExecutarTudoAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // encerramento normal do host — não é falha de projeção.
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao rodar catch-up de projeções — fact tables podem ficar atrasadas até o próximo ciclo ({Intervalo}).",
                options.Value.ProjectionCatchUpInterval);
        }
    }
}
