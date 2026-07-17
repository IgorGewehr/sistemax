using SistemaX.Modules.Abstractions.Consultor;

namespace SistemaX.Modules.Abstractions.Tests.Consultor;

/// <summary>
/// <see cref="NarradorTemplate"/> é o "piso" grátis e determinístico do Super Consultor (ADR-0005
/// §7) — nesta rodada é o ÚNICO narrador registrado (nenhuma chamada a LLM, de propósito). Estes
/// testes travam que ele é puramente um empacotador: a frase final é sempre
/// <see cref="ConsultorFato.TemplateFallback"/> verbatim, a origem é sempre
/// <see cref="ConsultorNarracaoOrigem.Template"/>, e nada (facts/drill/score) se perde no caminho.
/// </summary>
public class NarradorTemplateTests
{
    private static ConsultorFato NovoFato(string ruleId, string frase, int score = 100) => new(
        Modulo: "financeiro",
        RuleId: ruleId,
        Tela: "visao-geral",
        Score: score,
        Facts: new Dictionary<string, string> { ["x"] = "1" },
        TemplateFallback: frase,
        Drill: new DrillTarget("visao-geral"));

    [Fact]
    public async Task NarrarAsync_DevolveTemplateFallbackVerbatim_ComOrigemTemplate()
    {
        var narrador = new NarradorTemplate();
        var fato = NovoFato("fin.teste", "Frase pronta e determinística.");

        var resultado = await narrador.NarrarAsync([fato]);

        var insight = Assert.Single(resultado);
        Assert.Equal(fato.TemplateFallback, insight.Frase);
        Assert.Equal(ConsultorNarracaoOrigem.Template, insight.Origem);
        Assert.Equal(fato.Modulo, insight.Modulo);
        Assert.Equal(fato.RuleId, insight.RuleId);
        Assert.Equal(fato.Tela, insight.Tela);
        Assert.Equal(fato.Score, insight.Score);
        Assert.Same(fato.Facts, insight.Facts);
        Assert.Equal(fato.Drill, insight.Drill);
    }

    [Fact]
    public async Task NarrarAsync_ComLoteDeVariosFatos_PreservaOrdemDeEntrada()
    {
        var narrador = new NarradorTemplate();
        var fatos = new[]
        {
            NovoFato("fin.a", "Primeira frase."),
            NovoFato("fin.b", "Segunda frase."),
            NovoFato("fin.c", "Terceira frase."),
        };

        var resultado = await narrador.NarrarAsync(fatos);

        Assert.Equal(["fin.a", "fin.b", "fin.c"], resultado.Select(r => r.RuleId));
    }

    [Fact]
    public async Task NarrarAsync_ComListaVazia_DevolveListaVazia_NaoCrasha()
    {
        var narrador = new NarradorTemplate();

        var resultado = await narrador.NarrarAsync([]);

        Assert.Empty(resultado);
    }
}
