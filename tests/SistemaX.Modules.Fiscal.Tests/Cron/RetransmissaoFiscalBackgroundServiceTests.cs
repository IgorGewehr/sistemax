using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Infrastructure.Cron;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Tests.Fakes;

namespace SistemaX.Modules.Fiscal.Tests.Cron;

/// <summary>
/// Exercita o PRÓPRIO <see cref="RetransmissaoFiscalBackgroundService"/> — loop de intervalo,
/// catch-up imediato, fail-open e cancelamento gracioso — mesmo molde de
/// <c>AvaliarParcelasVencidasBackgroundServiceTests</c> (Financeiro.Tests). A correção do PRÓPRIO
/// <c>RetransmitirDocumentosPendentesUseCase</c> (retry, backoff, limite de tentativas) é provada
/// em <c>RetransmitirDocumentosPendentesUseCaseTests</c>.
/// </summary>
public sealed class RetransmissaoFiscalBackgroundServiceTests
{
    [Fact]
    public async Task Dispara_um_ciclo_imediatamente_no_start_sem_esperar_o_intervalo()
    {
        var scopeFactory = new FakeScopeFactory();
        var servico = CriarServico(scopeFactory, new FakeTenants(), new FakeLogger(), TimeSpan.FromSeconds(5));

        await servico.StartAsync(CancellationToken.None);
        try
        {
            await AguardarAsync(() => scopeFactory.Chamadas >= 1, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await servico.StopAsync(CancellationToken.None);
            servico.Dispose();
        }
    }

    [Fact]
    public async Task Roda_periodicamente_no_intervalo_configurado()
    {
        var scopeFactory = new FakeScopeFactory();
        var servico = CriarServico(scopeFactory, new FakeTenants(), new FakeLogger(), TimeSpan.FromMilliseconds(20));

        await servico.StartAsync(CancellationToken.None);
        try
        {
            await AguardarAsync(() => scopeFactory.Chamadas >= 3, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await servico.StopAsync(CancellationToken.None);
            servico.Dispose();
        }
    }

    [Fact]
    public async Task Fail_open_uma_excecao_num_ciclo_nao_derruba_o_loop_e_e_logada()
    {
        var tenants = new FakeTenants();
        tenants.LancarExcecaoNaChamadaDeNumero(2);
        var scopeFactory = new FakeScopeFactory();
        var logger = new FakeLogger();
        var servico = CriarServico(scopeFactory, tenants, logger, TimeSpan.FromMilliseconds(15));

        await servico.StartAsync(CancellationToken.None);
        try
        {
            await AguardarAsync(() => tenants.Chamadas >= 4, TimeSpan.FromSeconds(2));
            Assert.True(logger.ChamadasDeErro >= 1, "A falha do ciclo deveria ter sido logada como erro.");
        }
        finally
        {
            await servico.StopAsync(CancellationToken.None);
            servico.Dispose();
        }
    }

    [Fact]
    public async Task Cancelamento_gracioso_para_o_loop_sem_lancar_e_sem_novos_ciclos()
    {
        var scopeFactory = new FakeScopeFactory();
        var servico = CriarServico(scopeFactory, new FakeTenants(), new FakeLogger(), TimeSpan.FromMilliseconds(15));

        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => scopeFactory.Chamadas >= 1, TimeSpan.FromSeconds(2));

        await servico.StopAsync(CancellationToken.None);
        var chamadasAoParar = scopeFactory.Chamadas;

        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(chamadasAoParar, scopeFactory.Chamadas);

        servico.Dispose();
    }

    private static RetransmissaoFiscalBackgroundService CriarServico(
        FakeScopeFactory scopeFactory, FakeTenants tenants, FakeLogger logger, TimeSpan intervalo)
    {
        var options = Options.Create(new FiscalCronOptions { IntervaloRetransmissao = intervalo });
        return new RetransmissaoFiscalBackgroundService(scopeFactory, tenants, options, logger);
    }

    private static async Task AguardarAsync(Func<bool> condicao, TimeSpan timeout)
    {
        var expiraEm = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < expiraEm)
        {
            if (condicao()) return;
            await Task.Delay(10);
        }

        Assert.True(condicao(), "Condição não satisfeita dentro do timeout.");
    }

    /// <summary>Resolve um <see cref="RetransmitirDocumentosPendentesUseCase"/> real sobre um
    /// repositório in-memory vazio — nunca chama o gateway (nenhum documento pendente), então só
    /// observamos QUANTAS vezes e COM QUE COMPORTAMENTO o host dispara o ciclo.</summary>
    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        public int Chamadas { get; private set; }

        public IServiceScope CreateScope()
        {
            Chamadas++;
            var documentos = new InMemoryDocumentoFiscalRepository();
            var bus = new FakeIntegrationEventBus();
            var transmissor = new TransmitirDocumentoFiscalUseCase(documentos, bus, FakeGatewayEmissaoSefaz.Autorizando());
            var useCase = new RetransmitirDocumentosPendentesUseCase(documentos, transmissor, new DesistirDeNumeroUseCase(documentos, bus));
            return new FakeServiceScope(useCase);
        }
    }

    private sealed class FakeServiceScope(RetransmitirDocumentosPendentesUseCase useCase) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(useCase);

        public void Dispose()
        {
        }
    }

    private sealed class FakeServiceProvider(RetransmitirDocumentosPendentesUseCase useCase) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(RetransmitirDocumentosPendentesUseCase) ? useCase : null;
    }

    private sealed class FakeTenants : ITenantsDeInstalacao
    {
        private int _numeroDaChamadaQueDeveLancar = -1;

        public int Chamadas { get; private set; }

        public void LancarExcecaoNaChamadaDeNumero(int numero) => _numeroDaChamadaQueDeveLancar = numero;

        public Task<IReadOnlyList<string>> ObterBusinessIdsAsync(CancellationToken ct = default)
        {
            Chamadas++;
            if (Chamadas == _numeroDaChamadaQueDeveLancar)
                throw new InvalidOperationException("Falha simulada de resolução de tenants.");

            return Task.FromResult<IReadOnlyList<string>>(["tenant-1"]);
        }
    }

    private sealed class FakeLogger : ILogger<RetransmissaoFiscalBackgroundService>
    {
        public int ChamadasDeErro { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error) ChamadasDeErro++;
        }
    }
}
