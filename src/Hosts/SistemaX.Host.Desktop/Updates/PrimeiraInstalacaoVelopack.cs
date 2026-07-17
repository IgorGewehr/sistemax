namespace SistemaX.Host.Desktop.Updates;

/// <summary>
/// Gancho de primeira instalação (ADR-0004, decisão #7 / docs/build/empacotamento.md §9.3) —
/// registrado em <c>Program.cs</c> via <c>VelopackApp.Build().OnFirstRun(...)</c>. Este callback
/// SÓ dispara quando o próprio Velopack detecta que acabou de instalar o app pela primeira vez
/// (nunca em <c>dotnet run</c>/dev, nunca em CI/testes) — é exatamente o mecanismo que a ADR
/// propõe como opção (a) para resolver o risco mais grave do instalador: sob Velopack,
/// <c>AppContext.BaseDirectory</c> é a pasta VERSIONADA do app, trocada a cada auto-update. Se
/// <c>SISTEMAX_DATA_DIR</c> não apontar para um lugar estável, um auto-update apaga/orfaniza
/// <c>config.json</c>/<c>sistemax.db</c>/<c>logs/</c> da loja.
/// </summary>
public static class PrimeiraInstalacaoVelopack
{
    /// <summary>
    /// Fixa <c>SISTEMAX_DATA_DIR</c> para <c>%ProgramData%\SistemaX</c> — de máquina (persiste
    /// entre re-logins/relançamentos futuros do atalho) e de processo (efeito imediato neste
    /// mesmo boot, já que <see cref="HostConfigLoader.CarregarOuCriar"/> roda logo em seguida, no
    /// mesmo processo). Nunca sobrescreve um valor já definido — reinstalação/reparo não perde uma
    /// configuração manual.
    /// </summary>
    public static void ConfigurarDiretorioDeDadosDeProducao()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Velopack só instala "de verdade" no Windows (é o único alvo real do Host.Desktop,
            // ver CLAUDE.md/ADR-0004) — este guard só evita PlatformNotSupportedException do
            // EnvironmentVariableTarget.Machine se o hook algum dia disparar fora dele.
            return;
        }

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMAX_DATA_DIR", EnvironmentVariableTarget.User))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SISTEMAX_DATA_DIR")))
        {
            return;
        }

        var diretorio = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SistemaX");

        Directory.CreateDirectory(diretorio);

        // User (não Machine): instalação Velopack é per-user, sem UAC/admin (ADR-0004 decisão #1)
        // — HKLM/Machine exigiria elevação e quebraria essa promessa. Persiste para o PRÓXIMO
        // lançamento pelo atalho; o SetEnvironmentVariable sem target abaixo cobre o boot ATUAL.
        Environment.SetEnvironmentVariable("SISTEMAX_DATA_DIR", diretorio, EnvironmentVariableTarget.User);
        Environment.SetEnvironmentVariable("SISTEMAX_DATA_DIR", diretorio);
    }
}
