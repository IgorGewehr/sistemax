using SistemaX.Modules.Abstractions.Consultor;

namespace SistemaX.Modules.Abstractions.Tests.Consultor;

/// <summary>
/// <see cref="ConsultorRanking"/> é puramente ordinal (ADR-0005 §3.5 passo 3) — sem LLM, sem
/// aleatoriedade. Trava as duas garantias do contrato: (1) pelo menos 1 fato por TELA distinta
/// entra antes de completar o resto do orçamento por score global; (2) empate de score desempata
/// por <see cref="ConsultorFato.RuleId"/> ordinal, nunca por ordem de inserção.
/// </summary>
public class ConsultorRankingTests
{
    private static ConsultorFato Fato(string ruleId, string tela, int score) => new(
        "financeiro", ruleId, tela, score, new Dictionary<string, string>(), $"frase-{ruleId}", null);

    [Fact]
    public void Selecionar_GaranteUmFatoPorTela_MesmoComScoreBaixoPertoDeOutraTelaMaisAlta()
    {
        var fatos = new[]
        {
            Fato("fin.a", "visao-geral", 9_000),
            Fato("fin.b", "visao-geral", 8_000),
            Fato("fin.c", "fluxo-caixa", 10), // score baixo, mas ÚNICO fato desta tela
        };

        var selecionados = ConsultorRanking.Selecionar(fatos, topN: 2);

        Assert.Contains(selecionados, f => f.RuleId == "fin.a"); // maior score global
        Assert.Contains(selecionados, f => f.RuleId == "fin.c"); // garantia por tela
        Assert.DoesNotContain(selecionados, f => f.RuleId == "fin.b");
    }

    [Fact]
    public void Selecionar_DesempataScoreIgualPorRuleIdOrdinal()
    {
        var fatos = new[]
        {
            Fato("fin.zebra", "visao-geral", 500),
            Fato("fin.abacate", "fluxo-caixa", 500),
        };

        var selecionados = ConsultorRanking.Selecionar(fatos, topN: 1);

        Assert.Equal("fin.abacate", Assert.Single(selecionados).RuleId);
    }

    [Fact]
    public void Selecionar_ComTopNZeroOuListaVazia_DevolveVazio_NaoCrasha()
    {
        var fatos = new[] { Fato("fin.a", "visao-geral", 100) };

        Assert.Empty(ConsultorRanking.Selecionar(fatos, topN: 0));
        Assert.Empty(ConsultorRanking.Selecionar([], topN: 8));
    }

    [Fact]
    public void Selecionar_PreenchendoRestanteDoOrcamento_PegaMaioresScoresGlobaisRestantes()
    {
        var fatos = new[]
        {
            Fato("fin.a", "visao-geral", 9_000),
            Fato("fin.b", "visao-geral", 8_000),
            Fato("fin.c", "fluxo-caixa", 10),
            Fato("fin.d", "recorrentes", 7_000),
        };

        // topN=3: garante 1/tela (a, c, d — cada tela tem seu melhor) e já fecha o orçamento.
        var selecionados = ConsultorRanking.Selecionar(fatos, topN: 3);

        Assert.Equal(3, selecionados.Count);
        Assert.Equal(["fin.a", "fin.d", "fin.c"], selecionados.Select(f => f.RuleId));
    }
}
