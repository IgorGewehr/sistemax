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
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Financeiro.Infrastructure.Seed;
using SistemaX.Modules.Vendas.Application;
using SistemaX.Modules.Vendas.Infrastructure;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia.Application;
using SistemaX.Verticals.Assistencia.Application.Ports;
using SistemaX.Verticals.Assistencia.Infrastructure;

namespace SistemaX.Host.Desktop.Tests;

/// <summary>
/// Prova de ponta-a-ponta do <see cref="DemoSeeder"/> (item #48 do roadmap): monta o MESMO grafo
/// de módulos que <c>SistemaXHost.RegistrarModulos</c> liga em produção (Financeiro/Vendas/
/// Estoque/Assistência + infra local SQLite real, num arquivo temporário isolado por teste —
/// nunca o <c>AppContext.BaseDirectory/data</c> hardcoded que o composition root de produção usa),
/// roda o seeder 2× e prova (1) idempotência — nenhuma coleção cresce na segunda passada — e
/// (2) que os read-models que os mockups <c>projeto.html</c>/<c>roi-negocio.html</c> alvejam voltam
/// com número real, não vazio.
/// </summary>
public sealed class DemoSeederTests : IAsyncLifetime
{
    private const string BusinessId = "loja-demo-teste-seeder";

    private string _dataDir = null!;
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "sistemax-demoseeder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["persistencia"] = "sqlite" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var contexto = new ModuleContext(CamadaExecucao.Pdv, configuracao);
        var registry = new ModuleRegistry()
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

        // Mesmo gesto documentado em LocalDatabaseBootstrapper: chamada manual pra host sem
        // Generic Host — aplica TODAS as migrações de schema (infra + Financeiro/Vendas/Estoque/
        // Assistência) antes de qualquer repositório SQLite ser tocado.
        await _provider.GetRequiredService<LocalDatabaseBootstrapper>().BootstrapAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        try { Directory.Delete(_dataDir, recursive: true); } catch { /* best-effort — não é o objeto sob teste */ }
    }

    [Fact]
    public async Task SemearAsync_RodandoDuasVezes_NaoDuplicaNadaEOsPaineisVoltamComNumeroReal()
    {
        await FinanceiroBootstrapSeeder.SemearAsync(_provider, BusinessId);

        await DemoSeeder.SemearAsync(_provider, BusinessId);
        var contagemAposPrimeiraRodada = await ContarEntidadesChaveAsync();

        await DemoSeeder.SemearAsync(_provider, BusinessId);
        var contagemAposSegundaRodada = await ContarEntidadesChaveAsync();

        Assert.Equal(contagemAposPrimeiraRodada, contagemAposSegundaRodada);

        // Força o catch-up de projeções (fato_custo_diario/fato_recebiveis) síncrono — em produção
        // isso roda em background a cada ProjectionCatchUpInterval; aqui precisamos do dado fresco
        // na hora, sem esperar o timer.
        await _provider.GetRequiredService<ProjectionRunner>().ExecutarTudoAsync(CancellationToken.None);

        await AssertPainelDeProjetoDigiSatTemNumeroRealAsync();
        await AssertDrePorCorrenteTemAsTresCorrentesAsync();
        await AssertRoiDoNegocioTemInvestimentoRealAsync();
        await AssertComercioEServicoGeraramReceitaAsync();
    }

    /// <summary>Snapshot das contagens que provam idempotência — cobre cada técnica de guarda
    /// descrita no cabeçalho do <see cref="DemoSeeder"/> (natural-key, cron idempotente, KV flag).</summary>
    private async Task<(int Projetos, int Ativos, int Assinaturas, int Aportes, int OrdensDeServico, int ContasAReceber, int ContasAPagar, int Produtos)> ContarEntidadesChaveAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var projetos = (await sp.GetRequiredService<IProjetoRepository>().ListarAsync(BusinessId, incluirArquivados: true)).Count;
        var ativos = (await sp.GetRequiredService<IAtivoDeCapitalRepository>().ListarAsync(BusinessId)).Count;
        var assinaturas = (await sp.GetRequiredService<IAssinaturaRepository>().ListarAsync(BusinessId)).Count;
        var aportes = (await sp.GetRequiredService<IAporteDeCapitalRepository>().ListarAsync(BusinessId)).Count;
        var ordens = (await sp.GetRequiredService<IOrdemDeServicoRepository>().ListarAsync(BusinessId)).Count;
        var umAnoAtras = DateTimeOffset.UtcNow.AddYears(-1);
        var contasAReceber = (await sp.GetRequiredService<IContaAReceberRepository>().ListarPorCompetenciaAsync(BusinessId, umAnoAtras, DateTimeOffset.UtcNow)).Count;
        var contasAPagar = (await sp.GetRequiredService<IContaAPagarRepository>().ListarPorCompetenciaAsync(BusinessId, umAnoAtras, DateTimeOffset.UtcNow)).Count;
        var produtos = (await sp.GetRequiredService<IProdutoRepository>().ListarAsync(BusinessId)).Count;

        return (projetos, ativos, assinaturas, aportes, ordens, contasAReceber, contasAPagar, produtos);
    }

    private async Task AssertPainelDeProjetoDigiSatTemNumeroRealAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var digisat = await sp.GetRequiredService<IProjetoRepository>().BuscarPorNomeAsync(BusinessId, "DigiSat");
        Assert.NotNull(digisat);

        var painel = await sp.GetRequiredService<PainelDoProjetoService>().CalcularAsync(BusinessId, digisat!.Id);
        Assert.True(painel.Sucesso, painel.Falha ? painel.Erro.Mensagem : string.Empty);

        var resultado = painel.Valor;
        Assert.True(resultado.Receita.Mrr.Centavos > 0, "MRR do DigiSat deveria ser positivo — a assinatura foi tageada no projeto.");
        Assert.Equal(1, resultado.Receita.AssinaturasAtivas);
        Assert.True(resultado.Payback.InvestimentoTotalCentavos > 0, "Investimento (licença DigiSat) deveria ser positivo.");
        Assert.True(resultado.Capacidade.UnidadesTotais >= 5, "5 unidades de licença deveriam aparecer na capacidade.");
    }

    private async Task AssertDrePorCorrenteTemAsTresCorrentesAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var inicio = DateTimeOffset.UtcNow.AddMonths(-9);
        var fim = DateTimeOffset.UtcNow;
        var dre = await sp.GetRequiredService<DreGerencialService>().CalcularAsync(BusinessId, inicio, fim);

        Assert.Equal(3, dre.PorCorrente.Count);
        Assert.True(dre.ReceitaBruta.Centavos > 0, "Receita bruta do período deveria ser positiva.");
        Assert.Contains(dre.PorCorrente, c => c.Corrente == Modules.Financeiro.Domain.Comum.CorrenteDeReceita.Recorrente && c.ReceitaBruta.Centavos > 0);
        Assert.Contains(dre.PorCorrente, c => c.Corrente == Modules.Financeiro.Domain.Comum.CorrenteDeReceita.Servico && c.ReceitaBruta.Centavos > 0);
        Assert.Contains(dre.PorCorrente, c => c.Corrente == Modules.Financeiro.Domain.Comum.CorrenteDeReceita.Comercio && c.ReceitaBruta.Centavos > 0);
    }

    private async Task AssertRoiDoNegocioTemInvestimentoRealAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var roi = await sp.GetRequiredService<RoiDoNegocioService>().CalcularAsync(BusinessId);
        Assert.True(roi.Sucesso, roi.Falha ? roi.Erro.Mensagem : string.Empty);

        var resultado = roi.Valor;
        Assert.True(resultado.Investimento.TotalCentavos > 0, "Investimento total (ativos + aportes) deveria ser positivo.");
        Assert.True(resultado.Investimento.Bens >= 6, "6 bens deveriam compor o investimento (licença DigiSat + 5 imobilizados).");
        Assert.NotEmpty(resultado.Serie);
        Assert.True(resultado.Recuperacao.RecuperadoCentavos > 0, "Fluxo operacional acumulado + aportes deveria já ter recuperado parte do investimento.");
    }

    private async Task AssertComercioEServicoGeraramReceitaAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var ordens = await sp.GetRequiredService<IOrdemDeServicoRepository>().ListarAsync(BusinessId);
        Assert.Equal(2, ordens.Count);
        Assert.All(ordens, os => Assert.Equal(Verticals.Assistencia.StatusOrdemServico.Entregue, os.Status));

        var contasAReceber = await sp.GetRequiredService<IContaAReceberRepository>()
            .ListarPorCompetenciaAsync(BusinessId, DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow);
        Assert.Contains(contasAReceber, c => c.Corrente == Modules.Financeiro.Domain.Comum.CorrenteDeReceita.Comercio);
        Assert.Contains(contasAReceber, c => c.Corrente == Modules.Financeiro.Domain.Comum.CorrenteDeReceita.Servico);
    }
}
