using System.Text.Json;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>
/// Configuração desta instalação — <c>config.json</c> ao lado do executável (dev) ou em
/// <c>%ProgramData%/SistemaX</c> via <c>SISTEMAX_DATA_DIR</c> (produção Windows, ver plano de
/// produção §5.1/§6.5). Um wizard de primeiro-boot na UI (nome da loja, PIN do admin) fica pro
/// resto da F1 — hoje o primeiro boot cria um default de DEV (PIN "1234") sozinho.
///
/// <see cref="PinAdminHash"/>/<see cref="PinAdminSalt"/> NÃO são mais usados para autenticar
/// login (ADR-0003 §2/§5 — isso agora é <c>Usuario.PinHash</c>/<c>PinSalt</c> por PESSOA, via
/// <c>AutenticarPorPinUseCase</c>). Os campos ficam aqui, intocados, para a F1 do ADR-0003 §7
/// (migração do PIN único existente + PIN de recuperação/break-glass) reaproveitar — fora de
/// escopo desta fatia.
/// </summary>
/// <param name="AtualizacaoFeedUrl">URL do feed de releases Velopack (ex.:
/// <c>https://updates.sistemax.com.br/win/stable/</c>, ver `vpk pack --outputDir`/
/// docs/build/empacotamento.md §7). <c>null</c> por padrão — SEM feed configurado, a atualização
/// automática fica DESLIGADA (ver <see cref="Updates.IServicoDeAtualizacao"/>): nunca inventamos
/// um feed default.</param>
/// <param name="AtualizacaoCanal">Canal do feed (`stable`/`beta`, ADR-0004 decisão #5).
/// <c>null</c> usa o canal default do próprio feed.</param>
public sealed record HostConfig(
    string InstalacaoId,
    string BusinessId,
    string NomeLoja,
    int Porta,
    string LogLevel,
    string Persistencia,
    string PinAdminHash,
    string PinAdminSalt,
    string? UiUrl,
    string? AtualizacaoFeedUrl = null,
    string? AtualizacaoCanal = null);

/// <summary>Carrega ou cria <c>config.json</c>. Aplica overrides de variável de ambiente por
/// cima — úteis em dev (porta fixa pro proxy do Vite, UI URL) sem precisar editar o arquivo.</summary>
public static class HostConfigLoader
{
    private const string PinPadraoDev = "1234";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static (HostConfig Config, string CaminhoArquivo, string DiretorioDados) CarregarOuCriar()
    {
        var diretorioDados = Environment.GetEnvironmentVariable("SISTEMAX_DATA_DIR") ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(diretorioDados);

        var caminho = Path.Combine(diretorioDados, "config.json");

        var config = File.Exists(caminho)
            ? LerArquivo(caminho)
            : CriarPadrao(caminho);

        // Overrides de ambiente — nunca persistidos de volta no arquivo (são da EXECUÇÃO, não da
        // instalação).
        if (int.TryParse(Environment.GetEnvironmentVariable("SISTEMAX_PORT"), out var portaOverride))
        {
            config = config with { Porta = portaOverride };
        }

        var uiUrlOverride = Environment.GetEnvironmentVariable("SISTEMAX_UI_URL");
        if (!string.IsNullOrWhiteSpace(uiUrlOverride))
        {
            config = config with { UiUrl = uiUrlOverride };
        }

        // Override de feed/canal de update por variável de ambiente — mesma lógica de Porta/UiUrl
        // acima: útil pra apontar uma loja-piloto pro canal `beta` (ou trocar o host do feed) sem
        // editar `config.json` manualmente em cada instalação. Nunca persistido de volta no
        // arquivo (é da EXECUÇÃO, não da instalação) e nunca um default inventado — sem isto E sem
        // `AtualizacaoFeedUrl` no config.json, a atualização automática continua desligada.
        var feedOverride = Environment.GetEnvironmentVariable("SISTEMAX_UPDATE_FEED_URL");
        if (!string.IsNullOrWhiteSpace(feedOverride))
        {
            config = config with { AtualizacaoFeedUrl = feedOverride };
        }

        var canalOverride = Environment.GetEnvironmentVariable("SISTEMAX_UPDATE_CHANNEL");
        if (!string.IsNullOrWhiteSpace(canalOverride))
        {
            config = config with { AtualizacaoCanal = canalOverride };
        }

        return (config, caminho, diretorioDados);
    }

    private static HostConfig LerArquivo(string caminho)
    {
        var json = File.ReadAllText(caminho);
        return JsonSerializer.Deserialize<HostConfig>(json, JsonOptions)
            ?? throw new InvalidOperationException($"config.json inválido/vazio em '{caminho}'.");
    }

    private static HostConfig CriarPadrao(string caminho)
    {
        var (hash, salt) = PinHasher.Hash(PinPadraoDev);
        var config = new HostConfig(
            InstalacaoId: Guid.NewGuid().ToString("N"),
            BusinessId: "loja-demo",
            NomeLoja: "Loja Demo (dev)",
            Porta: 0,
            LogLevel: "Information",
            Persistencia: "sqlite",
            PinAdminHash: hash,
            PinAdminSalt: salt,
            UiUrl: null);

        File.WriteAllText(caminho, JsonSerializer.Serialize(config, JsonOptions));
        return config;
    }
}
