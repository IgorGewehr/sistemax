using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Host.Desktop.Bridge;
using SistemaX.Host.Desktop.Composition;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Infrastructure.Local.Projections;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Estoque.Application;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Infrastructure;
using SistemaX.Modules.Financeiro.Application;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Financeiro.Infrastructure.Seed;
using SistemaX.Modules.Identidade.Application;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Infrastructure;
using SistemaX.Modules.Identidade.Infrastructure.Seed;
using SistemaX.Modules.Vendas.Application;
using SistemaX.Modules.Vendas.Infrastructure;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia.Application;
using SistemaX.Verticals.Assistencia.Application.Ports;
using SistemaX.Verticals.Assistencia.Infrastructure;

namespace SistemaX.Host.Desktop.Tests;

/// <summary>
/// Fecha a lacuna entre "inspeção de código confirma" e "teste automatizado prova" (item #48 do
/// roadmap, pendência 2): <see cref="DemoSeederTests"/> roda contra
/// <c>BusinessId="loja-demo-teste-seeder"</c> — um id sintético só de teste. Este teste usa em vez
/// disso o valor LITERAL que <see cref="HostConfigLoader"/> — o MESMO carregador que
/// <c>Program.cs</c> chama no boot real — grava em <c>config.json</c> para uma instalação nova
/// (isolado via <c>SISTEMAX_DATA_DIR</c>, nunca o diretório real desta máquina), e reproduz a MESMA
/// SEQUÊNCIA de boot do <c>Program.cs</c> real (Identidade → Financeiro/contas → DemoSeeder) —
/// provando de ponta-a-ponta, e não por leitura de código, que o tenant demo de produção
/// (<c>loja-demo</c>, PIN 1234) sai do primeiro boot com o founder logável e os painéis do
/// Financeiro populados. Também recontagem explícita das entidades-chave antes/depois da 2ª
/// chamada de <see cref="DemoSeeder.SemearAsync"/> — mesma prova de idempotência de
/// <see cref="DemoSeederTests"/>, agora contra o BusinessId literal de produção.
/// </summary>
public sealed class DemoSeederTenantProducaoTests : IAsyncLifetime
{
    private string _dataDir = null!;
    private ServiceProvider _provider = null!;
    private string _businessIdDeProducao = null!;

    public async Task InitializeAsync()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "sistemax-demoseeder-producao-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);

        _businessIdDeProducao = CarregarBusinessIdDeProducaoIsolado();

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["persistencia"] = "sqlite" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var contexto = new ModuleContext(CamadaExecucao.Pdv, configuracao);
        var registry = new ModuleRegistry()
            .Adicionar(new IdentidadeModule())
            .Adicionar(new IdentidadeInfrastructureModule())
            .Adicionar(new FinanceiroModule())
            .Adicionar(new FinanceiroInfrastructureModule())
            .Adicionar(new VendasModule())
            .Adicionar(new VendasInfrastructureModule())
            .Adicionar(new EstoqueModule())
            .Adicionar(new EstoqueInfrastructureModule())
            .Adicionar(new AssistenciaModule())
            .Adicionar(new AssistenciaInfrastructureModule());
        registry.RegistrarTodos(services, contexto);

        services.AddSingleton<IIntegrationEventBus, InProcessIntegrationEventBus>();
        services.AddSistemaXLocalInfrastructure(o =>
        {
            o.DatabasePath = Path.Combine(_dataDir, "sistemax.db");
            o.BackupDirectory = Path.Combine(_dataDir, "backups");
        });
        services.AddSingleton(registry);

        _provider = services.BuildServiceProvider();
        await _provider.GetRequiredService<LocalDatabaseBootstrapper>().BootstrapAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best-effort — não é o objeto sob teste */ }
    }

    /// <summary>Chama o MESMO <see cref="HostConfigLoader.CarregarOuCriar"/> que <c>Program.cs</c>
    /// usa no boot real, isolado num diretório temporário via <c>SISTEMAX_DATA_DIR</c> (nunca o
    /// <c>config.json</c> real desta máquina) — prova, em vez de assumir, que o default de uma
    /// instalação nova é mesmo <c>businessId="loja-demo"</c>.</summary>
    private string CarregarBusinessIdDeProducaoIsolado()
    {
        var configDir = Path.Combine(_dataDir, "config");
        Directory.CreateDirectory(configDir);

        var anterior = Environment.GetEnvironmentVariable("SISTEMAX_DATA_DIR");
        Environment.SetEnvironmentVariable("SISTEMAX_DATA_DIR", configDir);
        try
        {
            var (hostConfig, _, _) = HostConfigLoader.CarregarOuCriar();
            return hostConfig.BusinessId;
        }
        finally
        {
            Environment.SetEnvironmentVariable("SISTEMAX_DATA_DIR", anterior);
        }
    }

    [Fact]
    public async Task TenantDeProducao_SaiDoBootComFounderLogavelEPaineisPopulados()
    {
        Assert.Equal("loja-demo", _businessIdDeProducao);

        // MESMA ordem do Program.cs real: bootstrap de Identidade (founder) → bootstrap de
        // contas/formas de pagamento → DemoSeeder. Roda 2× seguidas — mesma prova de idempotência
        // de DemoSeederTests, agora com o BusinessId literal de produção.
        await IdentidadeBootstrapSeeder.SemearFounderAsync(_provider, _businessIdDeProducao);
        await FinanceiroBootstrapSeeder.SemearAsync(_provider, _businessIdDeProducao);

        await DemoSeeder.SemearAsync(_provider, _businessIdDeProducao);
        var contagemAposPrimeiraRodada = await ContarEntidadesChaveAsync();

        await DemoSeeder.SemearAsync(_provider, _businessIdDeProducao);
        var contagemAposSegundaRodada = await ContarEntidadesChaveAsync();

        // Recontagem explícita (item #48 pendência 4) — mesma técnica de DemoSeederTests, agora
        // contra o BusinessId LITERAL de produção ("loja-demo"), não o id sintético de teste:
        // prova que nenhuma coleção-chave cresce na 2ª chamada do seeder também neste caminho.
        Assert.Equal(contagemAposPrimeiraRodada, contagemAposSegundaRodada);

        await AssertFounderLogaComPin1234Async();

        await _provider.GetRequiredService<ProjectionRunner>().ExecutarTudoAsync(CancellationToken.None);
        await AssertPainelDoProjetoDigiSatPopuladoAsync();
    }

    /// <summary>Mesmo snapshot de contagens de <see cref="DemoSeederTests.ContarEntidadesChaveAsync"/>,
    /// duplicado aqui (em vez de compartilhado) porque as duas classes montam grafos de DI
    /// diferentes (esta inclui Identidade) e isolar a prova por arquivo evita acoplamento entre
    /// testes de ponta-a-ponta independentes.</summary>
    private async Task<(int Projetos, int Ativos, int Assinaturas, int Aportes, int OrdensDeServico, int ContasAReceber, int ContasAPagar, int Produtos)> ContarEntidadesChaveAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var projetos = (await sp.GetRequiredService<IProjetoRepository>().ListarAsync(_businessIdDeProducao, incluirArquivados: true)).Count;
        var ativos = (await sp.GetRequiredService<IAtivoDeCapitalRepository>().ListarAsync(_businessIdDeProducao)).Count;
        var assinaturas = (await sp.GetRequiredService<IAssinaturaRepository>().ListarAsync(_businessIdDeProducao)).Count;
        var aportes = (await sp.GetRequiredService<IAporteDeCapitalRepository>().ListarAsync(_businessIdDeProducao)).Count;
        var ordens = (await sp.GetRequiredService<IOrdemDeServicoRepository>().ListarAsync(_businessIdDeProducao)).Count;
        var umAnoAtras = DateTimeOffset.UtcNow.AddYears(-1);
        var contasAReceber = (await sp.GetRequiredService<IContaAReceberRepository>().ListarPorCompetenciaAsync(_businessIdDeProducao, umAnoAtras, DateTimeOffset.UtcNow)).Count;
        var contasAPagar = (await sp.GetRequiredService<IContaAPagarRepository>().ListarPorCompetenciaAsync(_businessIdDeProducao, umAnoAtras, DateTimeOffset.UtcNow)).Count;
        var produtos = (await sp.GetRequiredService<IProdutoRepository>().ListarAsync(_businessIdDeProducao)).Count;

        return (projetos, ativos, assinaturas, aportes, ordens, contasAReceber, contasAPagar, produtos);
    }

    private async Task AssertFounderLogaComPin1234Async()
    {
        await using var scope = _provider.CreateAsyncScope();
        var login = await scope.ServiceProvider.GetRequiredService<AutenticarPorPinUseCase>()
            .ExecutarAsync(_businessIdDeProducao, "1234");

        Assert.True(login.Sucesso, login.Falha ? login.Erro.Mensagem : string.Empty);
        Assert.Equal("Administrador", login.Valor.Nome);
    }

    private async Task AssertPainelDoProjetoDigiSatPopuladoAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var digisat = await sp.GetRequiredService<IProjetoRepository>().BuscarPorNomeAsync(_businessIdDeProducao, "DigiSat");
        Assert.NotNull(digisat);

        var painel = await sp.GetRequiredService<PainelDoProjetoService>().CalcularAsync(_businessIdDeProducao, digisat!.Id);
        Assert.True(painel.Sucesso, painel.Falha ? painel.Erro.Mensagem : string.Empty);
        Assert.True(painel.Valor.Receita.Mrr.Centavos > 0, "MRR do DigiSat no tenant de produção deveria ser positivo.");
    }
}
