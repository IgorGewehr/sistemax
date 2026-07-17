using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;
using Serilog.Events;
using SistemaX.Host.Desktop.Bridge;
using SistemaX.Host.Desktop.Composition;
using SistemaX.Host.Desktop.Updates;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Infrastructure.Seed;
using SistemaX.Modules.Identidade.Infrastructure.Seed;
using Velopack;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// ADR-0004 decisão #8 — VelopackApp.Build().Run() TEM que ser a PRIMEIRA instrução executada no
// processo, antes de qualquer boot do host (HostConfigLoader, Kestrel, janela Photino). É assim
// que o Velopack intercepta, dentro do PRÓPRIO .exe, as invocações especiais que ele mesmo faz
// durante instalar/desinstalar/atualizar (criar/remover atalho, etc.) — se qualquer código do
// host rodar antes disso, uma instalação silenciosa pode abrir janela, escrever config.json fora
// de hora, ou travar o instalador.
//
// Sem argumento de linha de comando reconhecido pelo Velopack — TODO boot normal, incluindo
// `dotnet run` em dev, o binário publicado rodando fora de instalação Velopack, e os 836 testes
// do repo — isto é NO-OP e retorna na hora. Não abre janela, não toca em I/O, não afeta
// SISTEMAX_HEADLESS nem nenhum boot existente.
//
// OnFirstRun só dispara quando o PRÓPRIO Velopack detecta que acabou de instalar o app agora
// mesmo (nunca em dev) — é o gancho que fecha a decisão #7 do ADR-0004 (SISTEMAX_DATA_DIR fora da
// pasta versionada do app, ver docs/build/empacotamento.md §9.3): sem isso, um auto-update mais
// adiante trocaria a pasta do app e apagaria/orfanizaria o sistemax.db/config.json da loja.
VelopackApp.Build()
    .OnFirstRun(_ => PrimeiraInstalacaoVelopack.ConfigurarDiretorioDeDadosDeProducao())
    .Run();

// ─────────────────────────────────────────────────────────────────────────────────────────────
// F1a — Host.Desktop vira o BRIDGE real: Generic Host (WebApplication) + Kestrel embutido em
// 127.0.0.1 (porta efêmera por padrão) servindo /api/* + a janela Photino. Preserva o boot dos
// módulos do F0 (SistemaXHost) e o migrator/sistemax.db (AddSistemaXLocalInfrastructure, ligado
// dentro de RegistrarModulos). Ver docs/host-desktop-bridge.md e
// scratchpad/design/sistemax-production-plano.md §2/§5.1 para a especificação completa.
// ─────────────────────────────────────────────────────────────────────────────────────────────

var (hostConfig, configPath, dataDir) = HostConfigLoader.CarregarOuCriar();
var bootToken = Guid.NewGuid().ToString("N");
var iniciadoEm = DateTimeOffset.UtcNow;

// ContentRootPath explícito = pasta do binário (AppContext.BaseDirectory), nunca o cwd padrão.
// Sem isso, `dotnet run` usa o diretório do PROJETO (fonte) como content root — e wwwroot/ só
// existe no diretório de OUTPUT do build (é lá que o Target CopyWebDist copia web/dist para).
// Publicado (`dotnet <dll>`/binário standalone) já cai certo por padrão, mas fixar aqui deixa dev
// e prod consistentes.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// A configuração da instalação (config.json) alimenta o mesmo IConfiguration que os módulos
// recebem em IModuleContext.Configuracao — é como ComprasInfrastructureModule decide
// InMemory x Sqlite (ver ComprasInfrastructureModule.Registrar).
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["persistencia"] = hostConfig.Persistencia
});

builder.Host.UseSerilog((_, loggerConfig) =>
{
    var nivel = Enum.TryParse<LogEventLevel>(hostConfig.LogLevel, ignoreCase: true, out var parsed)
        ? parsed
        : LogEventLevel.Information;

    loggerConfig
        .MinimumLevel.Is(nivel)
        .Enrich.WithProperty("instalacaoId", hostConfig.InstalacaoId)
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(dataDir, "logs", "sistemax-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14);
});

// Kestrel — SÓ loopback. Porta efêmera (0) por padrão; SISTEMAX_PORT fixa uma (dev, pro proxy do
// Vite apontar sempre pro mesmo lugar). Nunca 0.0.0.0/*: ver plano de produção §2.3 — o bridge
// local não pode ser alcançável pela LAN (isso é papel do Store.Server na F4).
builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, hostConfig.Porta));

var registry = SistemaXHost.RegistrarModulos(builder.Services, CamadaExecucao.Pdv, builder.Configuration, hostConfig.BusinessId);

builder.Services.AddSingleton(hostConfig);
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<IServicoDeAtualizacao, ServicoDeAtualizacaoVelopack>();

var app = builder.Build();

app.UseMiddleware<BearerSessionMiddleware>();

var api = app.MapGroup("/api");
BridgeEndpoints.Mapear(api, hostConfig, bootToken, app.Services.GetRequiredService<SessionStore>(), iniciadoEm);

// Contrato IModuleEndpoints (F1a — item 3): o Host só ENUMERA, nunca conhece rota concreta de
// módulo nenhum — zero `if` aqui sobre qual módulo é qual.
foreach (var modulo in registry.ModulosAdicionados.OfType<IModuleEndpoints>())
{
    modulo.MapearEndpoints(api);
}

// SPA — servida de wwwroot/ (copiado de web/dist no build, ver Target CopyWebDist no .csproj).
// Em dev com SISTEMAX_UI_URL setado, a janela aponta direto pro Vite e isto fica sem uso (sem
// problema: só serve estático se alguém bater na raiz do Kestrel mesmo assim).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.StartAsync();

var enderecos = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
var urlBase = enderecos?.FirstOrDefault() ?? $"http://127.0.0.1:{hostConfig.Porta}";

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation(
    "SistemaX no ar. API: {Url}/api — janela: {Url}/?boot={BootToken} — config: {ConfigPath}",
    urlBase, urlBase, bootToken, configPath);

// Bootstrap de Identidade (ADR-0003 §3) — IDEMPOTENTE, roda em TODO boot (dev e produção, ao
// contrário do DemoSeeder abaixo): garante que a instalação NUNCA fica sem um founder pra logar,
// mesmo na primeira vez que o banco é criado. Antes do DemoSeeder de propósito — dado de negócio
// (Compras/Financeiro/Estoque) não deveria existir sem que o login já funcione.
await IdentidadeBootstrapSeeder.SemearFounderAsync(app.Services, hostConfig.BusinessId);
logger.LogInformation("Bootstrap de usuário founder aplicado (idempotente).");

// Bootstrap do domínio Bancário (docs/wiring/financeiro-telas-restantes.md §3) — IDEMPOTENTE,
// mesmo espírito do bootstrap de Identidade acima: sem conta/forma de pagamento cadastrada, a tela
// Bancário fica vazia e fato_recebiveis nunca encontra MDR/lag pra aplicar.
await FinanceiroBootstrapSeeder.SemearAsync(app.Services, hostConfig.BusinessId);
logger.LogInformation("Bootstrap de contas/formas de pagamento aplicado (idempotente).");

// Semente idempotente pros dois endpoints reais da F1a terem dado de verdade (ver DemoSeeder —
// temporário até a UI ganhar os formulários de cadastro).
await DemoSeeder.SemearAsync(app.Services, hostConfig.BusinessId);
logger.LogInformation("Semente de dados aplicada (idempotente). Pronto para requisições.");

// Atualização automática (ADR-0004) — fire-and-forget, NUNCA bloqueia o boot/abertura da janela.
// IServicoDeAtualizacao já é honesto por conta própria: sem feed configurado em HostConfig, isto
// só loga "desabilitado" e retorna; nenhuma exceção escapa (try/catch interno), então não há risco
// de tarefa não observada aqui.
_ = app.Services.GetRequiredService<IServicoDeAtualizacao>().VerificarEAplicarAsync(CancellationToken.None);

// Modo dev: SISTEMAX_UI_URL aponta a janela pro Vite (HMR) em vez do bundle estático servido
// acima — ver plano de produção §2.4.
var urlJanela = hostConfig.UiUrl ?? urlBase;
var abriuJanela = PhotinoWindowLauncher.TentarAbrirEAguardar(urlJanela, bootToken, logger);

if (abriuJanela)
{
    // Janela fechou — desliga o host graciosamente.
    await app.StopAsync();
}
else
{
    logger.LogInformation("Sem janela — servidor segue no ar. Ctrl+C para encerrar.");
    await app.WaitForShutdownAsync();
}
