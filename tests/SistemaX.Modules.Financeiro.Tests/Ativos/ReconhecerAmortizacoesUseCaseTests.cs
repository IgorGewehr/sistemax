using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// <see cref="ReconhecerAmortizacoesUseCase"/> — clone estrutural de
/// <c>GerarCobrancasAssinaturasUseCase</c> (docs/financeiro/design-analise-por-projeto.md §4.5):
/// catch-up idempotente, dupla rede (cursor no domínio + <c>BuscarPorOrigemAsync</c>).
/// </summary>
public sealed class ReconhecerAmortizacoesUseCaseTests
{
    private const string Biz = "loja-1";

    [Fact]
    public async Task ExecutarAsync_DigiSat_PrimeiroMesReconhece19153Centavos()
    {
        var ativos = new InMemoryAtivoDeCapitalRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var eventos = new FakeIntegrationEventBus();
        var useCase = new ReconhecerAmortizacoesUseCase(ativos, lancamentos, eventos);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 36,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), quantidadeUnidades: 5, projetoId: "projeto-digisat").Valor;
        await ativos.SalvarAsync(ativo);

        var agora = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var reconhecidas = await useCase.ExecutarAsync(Biz, agora);

        Assert.Equal(1, reconhecidas);
        var relido = await ativos.ObterPorIdAsync(Biz, ativo.Id);
        Assert.NotNull(relido!.UltimaCompetenciaReconhecida);
        Assert.Equal(new DateOnly(2026, 7, 1), new DateOnly(relido.UltimaCompetenciaReconhecida!.Value.Year, relido.UltimaCompetenciaReconhecida.Value.Month, 1));

        var lancamento = await lancamentos.BuscarPorOrigemAsync(Biz, $"financeiro.amortizacao:{ativo.Id}:202607");
        Assert.NotNull(lancamento);
        Assert.Equal(19_153, lancamento!.TotalDebito.Centavos);

        var evento = Assert.Single(eventos.Publicados.OfType<SistemaX.Modules.Abstractions.CustoAmortizadoReconhecido>());
        Assert.Equal(19_153, evento.ValorCentavos);
        Assert.Equal("projeto-digisat", evento.ProjetoId);
        Assert.Equal("2026-07", evento.Competencia);
    }

    [Fact]
    public async Task ExecutarAsync_RodarDuasVezesNoMesmoMes_NaoDuplicaLancamentoNemEvento()
    {
        var ativos = new InMemoryAtivoDeCapitalRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var eventos = new FakeIntegrationEventBus();
        var useCase = new ReconhecerAmortizacoesUseCase(ativos, lancamentos, eventos);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 36,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), quantidadeUnidades: 5).Valor;
        await ativos.SalvarAsync(ativo);

        var agora = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        await useCase.ExecutarAsync(Biz, agora);
        var reconhecidasSegundaRodada = await useCase.ExecutarAsync(Biz, agora);

        Assert.Equal(0, reconhecidasSegundaRodada); // replay não gera novo reconhecimento
        Assert.Single(eventos.Publicados); // nenhum evento duplicado

        var relido = await ativos.ObterPorIdAsync(Biz, ativo.Id);
        Assert.Equal(new DateOnly(2026, 8, 1), relido!.ProximaCompetenciaDevida); // cursor não regrediu nem duplicou
    }

    [Fact]
    public async Task ExecutarAsync_CatchUpDeVariosMeses_ReconheceTodosAteOLimite()
    {
        var ativos = new InMemoryAtivoDeCapitalRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var eventos = new FakeIntegrationEventBus();
        var useCase = new ReconhecerAmortizacoesUseCase(ativos, lancamentos, eventos);

        var ativo = AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 36,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), quantidadeUnidades: 5).Valor;
        await ativos.SalvarAsync(ativo);

        // Cron nunca rodou — catch-up de janeiro a julho (7 competências) numa rodada só.
        var agora = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var reconhecidas = await useCase.ExecutarAsync(Biz, agora);

        Assert.Equal(7, reconhecidas);
        var relido = await ativos.ObterPorIdAsync(Biz, ativo.Id);
        Assert.Equal(new DateOnly(2026, 8, 1), relido!.ProximaCompetenciaDevida);
    }
}
