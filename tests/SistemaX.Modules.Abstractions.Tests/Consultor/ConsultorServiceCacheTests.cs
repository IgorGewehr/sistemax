using SistemaX.Modules.Abstractions.Consultor;

namespace SistemaX.Modules.Abstractions.Tests.Consultor;

/// <summary>
/// <see cref="ConsultorService"/> orquestra provider(s) → ranking → cache-por-hash → narrador
/// (ADR-0005 §3.5 passo 3: "se sha256(Facts) == hash do último narrado da mesma rule → reusa frase
/// antiga, custo 0"). Estes testes travam o CACHE especificamente — com um narrador que CONTA
/// quantos fatos efetivamente narrou, provamos que fatos com os MESMOS <c>Facts</c> não são
/// renarrados, e que mudar <c>Facts</c> invalida o cache (nunca serve frase desatualizada).
/// </summary>
public class ConsultorServiceCacheTests
{
    private sealed class ProviderFixo(IReadOnlyList<ConsultorFato> fatos) : IConsultorFactProvider
    {
        public Task<IReadOnlyList<ConsultorFato>> ColetarAsync(PeriodoRef periodo, CancellationToken ct = default)
            => Task.FromResult(fatos);
    }

    private sealed class NarradorContador : IConsultorNarrador
    {
        public int TotalDeFatosNarrados { get; private set; }

        public Task<IReadOnlyList<ConsultorInsightNarrado>> NarrarAsync(IReadOnlyList<ConsultorFato> fatos, CancellationToken ct = default)
        {
            TotalDeFatosNarrados += fatos.Count;
            IReadOnlyList<ConsultorInsightNarrado> resultado = fatos
                .Select(f => new ConsultorInsightNarrado(f.Modulo, f.RuleId, f.Tela, f.Score, f.TemplateFallback, ConsultorNarracaoOrigem.Template, f.Facts, f.Drill))
                .ToList();
            return Task.FromResult(resultado);
        }
    }

    private static ConsultorFato Fato(string ruleId, string valor) => new(
        "financeiro", ruleId, "visao-geral", 100,
        new Dictionary<string, string> { ["valor"] = valor }, $"frase-{ruleId}-{valor}", null);

    [Fact]
    public async Task GerarInsightsAsync_ChamadoDuasVezesComMesmosFatos_NaoRenarraNaSegundaVez()
    {
        var provider = new ProviderFixo([Fato("fin.a", "10")]);
        var narrador = new NarradorContador();
        var cache = new InMemoryConsultorInsightCache();
        var service = new ConsultorService([provider], narrador, cache);
        var periodo = new PeriodoRef("business-1", new DateOnly(2026, 3, 15));

        var primeira = await service.GerarInsightsAsync(periodo);
        var segunda = await service.GerarInsightsAsync(periodo);

        Assert.Equal(1, narrador.TotalDeFatosNarrados); // só narrou na primeira vez
        Assert.Equal(primeira.Single().Frase, segunda.Single().Frase);
        Assert.Equal(ConsultorNarracaoOrigem.Template, segunda.Single().Origem);
    }

    [Fact]
    public async Task GerarInsightsAsync_ComFatosMudandoDeValor_InvalidaCacheERenarra()
    {
        var narrador = new NarradorContador();
        var cache = new InMemoryConsultorInsightCache();
        var periodo = new PeriodoRef("business-1", new DateOnly(2026, 3, 15));

        var providerV1 = new ProviderFixo([Fato("fin.a", "10")]);
        var serviceV1 = new ConsultorService([providerV1], narrador, cache);
        await serviceV1.GerarInsightsAsync(periodo);

        // Mesma rule, MESMO cache, mas o FATO mudou (valor "10" -> "20") — hash muda, deve renarrar.
        var providerV2 = new ProviderFixo([Fato("fin.a", "20")]);
        var serviceV2 = new ConsultorService([providerV2], narrador, cache);
        var resultado = await serviceV2.GerarInsightsAsync(periodo);

        Assert.Equal(2, narrador.TotalDeFatosNarrados); // narrou nas DUAS vezes — fatos diferentes
        Assert.Equal("frase-fin.a-20", resultado.Single().Frase);
    }

    [Fact]
    public async Task GerarInsightsAsync_SemFatoNenhum_DevolveListaVazia_NaoCrasha()
    {
        var provider = new ProviderFixo([]);
        var service = new ConsultorService([provider], new NarradorContador(), new InMemoryConsultorInsightCache());
        var periodo = new PeriodoRef("business-1", new DateOnly(2026, 3, 15));

        var resultado = await service.GerarInsightsAsync(periodo);

        Assert.Empty(resultado);
    }
}
