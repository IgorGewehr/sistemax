using Microsoft.Extensions.Logging;
using SistemaX.Host.Desktop.Bridge;
using Velopack;

namespace SistemaX.Host.Desktop.Updates;

/// <summary>
/// Implementação real de <see cref="IServicoDeAtualizacao"/> via <c>UpdateManager</c> do
/// Velopack. Ver docs/build/empacotamento.md §9.4 (código ilustrativo original desta fatia) e
/// ADR-0004 decisão #5 (canais stable/beta).
/// </summary>
public sealed class ServicoDeAtualizacaoVelopack(HostConfig config, ILogger<ServicoDeAtualizacaoVelopack> logger)
    : IServicoDeAtualizacao
{
    public bool Habilitado => !string.IsNullOrWhiteSpace(config.AtualizacaoFeedUrl);

    public async Task VerificarEAplicarAsync(CancellationToken cancellationToken)
    {
        if (!Habilitado)
        {
            logger.LogInformation("Atualização automática desabilitada (sem feed configurado).");
            return;
        }

        try
        {
            var opcoes = string.IsNullOrWhiteSpace(config.AtualizacaoCanal)
                ? null
                : new UpdateOptions { ExplicitChannel = config.AtualizacaoCanal };

            var gerenciador = new UpdateManager(config.AtualizacaoFeedUrl!, opcoes);

            if (!gerenciador.IsInstalled)
            {
                // dev/dotnet run, ou binário não instalado via Velopack (ex.: portable/zip) —
                // nunca tenta checar update nesses casos.
                logger.LogInformation(
                    "Instância não foi instalada via Velopack — checagem de atualização pulada.");
                return;
            }

            var atualizacao = await gerenciador.CheckForUpdatesAsync().ConfigureAwait(false);
            if (atualizacao is null)
            {
                logger.LogInformation("Nenhuma atualização disponível (feed: {Feed}).", config.AtualizacaoFeedUrl);
                return;
            }

            await gerenciador.DownloadUpdatesAsync(atualizacao, cancelToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Atualização {Versao} baixada — aplica no próximo fechamento natural do app (nunca à força no meio de uma venda, ADR-0001).",
                atualizacao.TargetFullRelease.Version);

            // Só agenda para quando o app fechar sozinho (janela fechada/Ctrl+C) — nunca mata o
            // processo no meio de uma transação em curso.
            gerenciador.WaitExitThenApplyUpdates(atualizacao.TargetFullRelease, silent: true, restart: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Checagem/aplicação de atualização falhou — app segue normalmente sem ela.");
        }
    }
}
