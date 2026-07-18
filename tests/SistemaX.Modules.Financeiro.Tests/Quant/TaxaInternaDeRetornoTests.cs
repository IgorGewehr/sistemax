using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

/// <summary>Bisseção sobre o VPL (docs/financeiro/design-imobilizado-roi.md §7.6) — invariantes de
/// existência + a propriedade de convergência, nunca um valor mágico pinado (a raiz é conferível em
/// qualquer ferramenta externa, não decorada no teste).</summary>
public sealed class TaxaInternaDeRetornoTests
{
    private static List<long> SerieNominal24Meses()
    {
        var fluxos = new List<long> { -5_580_000 };
        for (var m = 1; m <= 4; m++) fluxos.Add(-300_000);
        for (var m = 5; m <= 24; m++) fluxos.Add(600_000);
        return fluxos;
    }

    [Fact]
    public void CenarioNominal_TirMensalPositiva_e_VplConvergeAZero()
    {
        var fluxos = SerieNominal24Meses();
        var resultado = TaxaInternaDeRetorno.Calcular(fluxos);

        Assert.NotNull(resultado.MensalPercent);
        Assert.NotNull(resultado.AnualizadaPercent);
        Assert.Null(resultado.MotivoIndefinida);
        Assert.True(resultado.MensalPercent > 0, "r* deveria ser positivo — VPL(0) = +52.200 > 0 no horizonte de 24m.");

        // Propriedade de convergência (não um valor mágico): recomputa VPL na taxa achada e checa
        // que fica dentro de uma tolerância PRÓXIMA de zero — não a tolerância interna de 0,5
        // centavo (essa é sobre o r* de PRECISÃO DUPLA usado durante a bisseção; aqui só temos
        // acesso ao percentual já arredondado a 2 casas decimais, e a série tem termos com peso
        // ~100^i perto de r=-1 — arredondar o r em 2 casas de PORCENTAGEM já desloca o VPL
        // recomputado em algumas centenas de centavos, sem que o algoritmo interno esteja errado).
        var r = (double)resultado.MensalPercent!.Value / 100.0;
        double vpl = 0;
        for (var i = 0; i < fluxos.Count; i++) vpl += fluxos[i] / Math.Pow(1 + r, i);
        Assert.True(Math.Abs(vpl) < 5_000, $"VPL recomputado do r* arredondado deveria ficar perto de zero, foi {vpl}.");
    }

    [Fact]
    public void CenarioNominal_TirAnualizadaEmFaixaDeSanidade()
    {
        // Faixa de sanidade (não um valor exato pinado, por design §7.6 — "o número exato sai da
        // bisseção e é conferível em qualquer ferramenta externa"): para esta série (25 meses,
        // burn nos meses 1-4, +R$6.000/mês dali em diante), a TIR mensal fica ~4,36% a.m., o que
        // credenciado no VPL(0)=+52.200 e no cruzamento do payback no mês 16.
        var resultado = TaxaInternaDeRetorno.Calcular(SerieNominal24Meses());

        Assert.NotNull(resultado.AnualizadaPercent);
        Assert.True(resultado.AnualizadaPercent > 0, "r* deveria ser positivo.");
        Assert.InRange(resultado.AnualizadaPercent!.Value, 40m, 90m);
    }

    [Fact]
    public void SemMudancaDeSinal_TodoPositivo_retorna_indefinida()
    {
        var resultado = TaxaInternaDeRetorno.Calcular([100, 200, 300]);

        Assert.Null(resultado.MensalPercent);
        Assert.Null(resultado.AnualizadaPercent);
        Assert.Equal("sem-mudanca-de-sinal", resultado.MotivoIndefinida);
    }

    [Fact]
    public void SemMudancaDeSinal_TodoNegativo_retorna_indefinida()
    {
        var resultado = TaxaInternaDeRetorno.Calcular([-100, -200, -300]);

        Assert.Equal("sem-mudanca-de-sinal", resultado.MotivoIndefinida);
    }

    [Fact]
    public void ComTrocaDeSinal_AnualizacaoEBateComMensalCompostaAo12DentroDaTolerancia()
    {
        var resultado = TaxaInternaDeRetorno.Calcular(SerieNominal24Meses());

        // Recomputa a partir do MENSAL já arredondado a 2 casas — double-rounding (duas
        // arredondagens em sequência) pode divergir na última casa decimal do anual; por isso a
        // tolerância, não igualdade exata (o cálculo interno usa o r* de precisão dupla, nunca o
        // percentual arredondado).
        var mensal = (double)resultado.MensalPercent!.Value / 100.0;
        var anualEsperada = (decimal)(Math.Pow(1 + mensal, 12) - 1) * 100m;

        Assert.True(
            Math.Abs(anualEsperada - resultado.AnualizadaPercent!.Value) < 0.1m,
            $"Esperava ~{anualEsperada}, obteve {resultado.AnualizadaPercent}.");
    }
}
