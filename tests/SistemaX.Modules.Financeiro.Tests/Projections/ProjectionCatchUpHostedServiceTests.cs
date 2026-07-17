using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local;
using SistemaX.Infrastructure.Local.Projections;
using SistemaX.Modules.Abstractions.Runtime;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// Exercita o PRÓPRIO <see cref="ProjectionCatchUpHostedService"/> — o loop de intervalo, o
/// fail-open em exceção e o cancelamento gracioso do <see cref="BackgroundService"/> — em
/// contraste com <see cref="ProjectionRunnerReprocessabilityTests"/>, que só prova o
/// <see cref="ProjectionRunner"/> subjacente (fold determinístico). Usa um
/// <see cref="IServiceScopeFactory"/> fake (nunca SQLite): cada chamada a
/// <see cref="ProjectionRunner.ExecutarTudoAsync"/> abre um escopo com zero <see cref="IProjection"/>
/// registrada, então o "ciclo de catch-up" em si é barato — só queremos observar QUANTAS vezes e
/// COM QUE COMPORTAMENTO o hosted service dispara esse ciclo.
/// </summary>
public sealed class ProjectionCatchUpHostedServiceTests
{
    [Fact]
    public async Task Dispara_um_ciclo_imediatamente_no_start_sem_esperar_o_intervalo()
    {
        var scopeFactory = new FakeScopeFactory();
        var logger = new FakeLogger();
        var servico = CriarServico(scopeFactory, logger, TimeSpan.FromSeconds(5));

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
        var logger = new FakeLogger();
        var servico = CriarServico(scopeFactory, logger, TimeSpan.FromMilliseconds(20));

        await servico.StartAsync(CancellationToken.None);
        try
        {
            // 3+ ciclos (1 imediato no boot + repetições no intervalo) provam que o loop
            // periódico de fato dispara de novo, não só a rodada inicial.
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
        var scopeFactory = new FakeScopeFactory();
        scopeFactory.LancarExcecaoNaChamadaDeNumero(2); // falha só no 2º ciclo
        var logger = new FakeLogger();
        var servico = CriarServico(scopeFactory, logger, TimeSpan.FromMilliseconds(15));

        await servico.StartAsync(CancellationToken.None);
        try
        {
            // Passa do ciclo que falha e continua rodando depois — é o fail-open: a exceção não
            // derruba o BackgroundService nem impede o próximo ciclo.
            await AguardarAsync(() => scopeFactory.Chamadas >= 4, TimeSpan.FromSeconds(2));

            Assert.True(logger.ChamadasDeErro >= 1, "A falha do ciclo deveria ter sido logada como erro.");
        }
        finally
        {
            // Se o fail-open não funcionasse, a exceção teria matado a task interna e StopAsync
            // relançaria — este finally provando "encerra sem lançar" é parte do próprio teste.
            await servico.StopAsync(CancellationToken.None);
            servico.Dispose();
        }
    }

    [Fact]
    public async Task Cancelamento_gracioso_para_o_loop_sem_lancar_e_sem_novos_ciclos()
    {
        var scopeFactory = new FakeScopeFactory();
        var logger = new FakeLogger();
        var servico = CriarServico(scopeFactory, logger, TimeSpan.FromMilliseconds(15));

        await servico.StartAsync(CancellationToken.None);
        await AguardarAsync(() => scopeFactory.Chamadas >= 1, TimeSpan.FromSeconds(2));

        // StopAsync não deve lançar — é o próprio contrato de encerramento gracioso do
        // BackgroundService (Task.Delay cancelado é capturado e vira "break", não exceção não
        // tratada que StopAsync precisaria relançar).
        await servico.StopAsync(CancellationToken.None);
        var chamadasAoParar = scopeFactory.Chamadas;

        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(chamadasAoParar, scopeFactory.Chamadas); // loop realmente parou, não só StopAsync retornou

        servico.Dispose();
    }

    private static ProjectionCatchUpHostedService CriarServico(
        FakeScopeFactory scopeFactory, FakeLogger logger, TimeSpan intervalo)
    {
        var runner = new ProjectionRunner(
            new LedgerNuncaUsadoNesteTeste(), new EstadoNuncaUsadoNesteTeste(), scopeFactory);
        var options = Options.Create(new LocalDatabaseOptions { ProjectionCatchUpInterval = intervalo });
        return new ProjectionCatchUpHostedService(runner, options, logger);
    }

    private static async Task AguardarAsync(Func<bool> condicao, TimeSpan timeout)
    {
        var expiraEm = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < expiraEm)
        {
            if (condicao())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condicao(), "Condição não satisfeita dentro do timeout.");
    }

    /// <summary>Nunca resolve nenhuma <see cref="IProjection"/> real — só conta quantas vezes
    /// <see cref="ProjectionRunner.ExecutarTudoAsync"/> abriu um escopo, e pode simular uma
    /// exceção numa chamada específica pra provar o fail-open.</summary>
    private sealed class FakeScopeFactory : IServiceScopeFactory
    {
        private int _numeroDaChamadaQueDeveLancar = -1;

        public int Chamadas { get; private set; }

        public void LancarExcecaoNaChamadaDeNumero(int numero) => _numeroDaChamadaQueDeveLancar = numero;

        public IServiceScope CreateScope()
        {
            Chamadas++;
            if (Chamadas == _numeroDaChamadaQueDeveLancar)
            {
                throw new InvalidOperationException("Falha simulada de catch-up de projeções.");
            }

            return new FakeServiceScope();
        }
    }

    private sealed class FakeServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeProjectionServiceProvider();

        public void Dispose()
        {
        }
    }

    /// <summary>Resolve <c>IEnumerable&lt;IProjection&gt;</c> vazio — o runner então não chama
    /// nenhum port (ledger/estado), o que mantém este teste puro de I/O.</summary>
    private sealed class FakeProjectionServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IEnumerable<IProjection>) ? Array.Empty<IProjection>() : null;
    }

    private sealed class LedgerNuncaUsadoNesteTeste : IIntegrationEventLedgerStore
    {
        public Task<bool> AppendAsync(
            string tipo, string tenantId, string payloadJson, DateTimeOffset ocorridoEm,
            string chaveIdempotencia, CancellationToken ct = default)
            => throw new NotSupportedException("Nenhuma IProjection registrada neste teste — o ledger nunca é lido.");

        public Task<IReadOnlyList<IntegrationEventLedgerEntry>> LerAPartirDoCursorAsync(
            long afterCursor, int maxBatchSize, CancellationToken ct = default)
            => throw new NotSupportedException("Nenhuma IProjection registrada neste teste — o ledger nunca é lido.");

        public Task<long> ObterUltimoCursorAsync(CancellationToken ct = default)
            => throw new NotSupportedException("Nenhuma IProjection registrada neste teste — o ledger nunca é lido.");
    }

    private sealed class EstadoNuncaUsadoNesteTeste : IProjectionStateStore
    {
        public Task<long> ObterCursorAsync(string nomeProjecao, CancellationToken ct = default)
            => throw new NotSupportedException("Nenhuma IProjection registrada neste teste — o cursor nunca é lido.");

        public Task SalvarCursorAsync(string nomeProjecao, long cursor, CancellationToken ct = default)
            => throw new NotSupportedException("Nenhuma IProjection registrada neste teste — o cursor nunca é salvo.");
    }

    /// <summary>Conta só os logs de nível <see cref="LogLevel.Error"/> — o suficiente para provar
    /// que o fail-open registra a falha em vez de engoli-la silenciosamente.</summary>
    private sealed class FakeLogger : ILogger<ProjectionCatchUpHostedService>
    {
        public int ChamadasDeErro { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                ChamadasDeErro++;
            }
        }
    }
}
