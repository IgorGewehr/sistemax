using System.Drawing;
using Microsoft.Extensions.Logging;
using Photino.NET;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>
/// Abre a janela desktop (Photino.NET — WebView2 no Windows, WKWebView no macOS/dev; ver plano de
/// produção §2.3) navegando para o bridge HTTP local com o boot-token na URL.
///
/// NON-FATAL por design: se a janela não puder abrir (ambiente sem display/sessão gráfica —
/// comum em CI/verificação automatizada), loga um aviso e devolve <c>false</c> — o servidor
/// <c>/api</c> continua no ar de qualquer jeito, que é o essencial da F1a (ver
/// <c>SISTEMAX_HEADLESS=1</c> pra pular a tentativa e ir direto pro modo servidor).
/// </summary>
public static class PhotinoWindowLauncher
{
    /// <summary>
    /// Tenta abrir a janela e BLOQUEIA a thread chamadora até ela fechar (roda o loop nativo de
    /// eventos do Photino) — chame por último em <c>Program.cs</c>, com o Kestrel já no ar.
    /// Retorna <c>false</c> sem lançar se não conseguir abrir.
    /// </summary>
    public static bool TentarAbrirEAguardar(string urlBase, string bootToken, ILogger logger)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SISTEMAX_HEADLESS"), "1", StringComparison.Ordinal))
        {
            logger.LogInformation(
                "SISTEMAX_HEADLESS=1 — pulando abertura da janela Photino. Servidor segue no ar em {Url}.", urlBase);
            return false;
        }

        try
        {
            var destino = new Uri($"{urlBase}/?boot={bootToken}");

            var window = new PhotinoWindow()
                .SetTitle("SistemaX")
                .SetUseOsDefaultSize(false)
                .SetSize(new Size(1366, 860))
                .Center()
                .Load(destino);

            logger.LogInformation("Janela Photino aberta em {Destino}.", destino);
            window.WaitForClose();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Não foi possível abrir a janela Photino (ambiente sem display/driver gráfico?). " +
                "O servidor /api segue respondendo em {Url} — use SISTEMAX_HEADLESS=1 pra pular esta " +
                "tentativa de propósito (verificação automatizada/CI).", urlBase);
            return false;
        }
    }
}
