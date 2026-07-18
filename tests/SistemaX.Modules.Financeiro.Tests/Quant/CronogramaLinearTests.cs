using SistemaX.Modules.Financeiro.Application.Quant;
using Xunit;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

/// <summary>
/// P1-5 (docs/financeiro/revisao-domain-fit-cnpj.md) — o LAR único de "espalhar um total exato em
/// N competências" (docs/financeiro/design-analise-por-projeto.md §4.2): receita diferida,
/// amortização e depreciação (fatias futuras) todos reusam <see cref="CronogramaLinear"/>.
/// </summary>
public class CronogramaLinearTests
{
    [Theory]
    [InlineData(689500, 36)]  // o exemplo do DigiSat na doc de design
    [InlineData(120000, 12)]  // anual redondo
    [InlineData(100, 3)]      // resto pequeno
    [InlineData(1, 5)]        // total menor que o número de meses
    [InlineData(0, 4)]        // total zero
    [InlineData(999999, 7)]
    public void Gerar_soma_das_parcelas_bate_com_o_total_exato(long total, int meses)
    {
        var cronograma = CronogramaLinear.Gerar(total, meses, new DateOnly(2026, 1, 1));

        Assert.Equal(meses, cronograma.Count);
        Assert.Equal(total, cronograma.Sum(c => c.ValorCentavos));
    }

    [Fact]
    public void Gerar_distribui_o_resto_para_as_primeiras_competencias()
    {
        // 689.500 ÷ 36 = 19.152,77… → floor 19.152, resto 28 → meses 1-28 recebem 19.153,
        // meses 29-36 recebem 19.152 (docs/financeiro/design-analise-por-projeto.md §4.3).
        var cronograma = CronogramaLinear.Gerar(689_500, 36, new DateOnly(2026, 1, 1));

        for (var i = 0; i < 28; i++) Assert.Equal(19_153, cronograma[i].ValorCentavos);
        for (var i = 28; i < 36; i++) Assert.Equal(19_152, cronograma[i].ValorCentavos);
    }

    [Fact]
    public void Gerar_competencias_sao_meses_consecutivos_a_partir_do_dia_1_do_mes_de_inicio()
    {
        var cronograma = CronogramaLinear.Gerar(1200, 4, new DateOnly(2026, 3, 15));

        Assert.Equal(new DateOnly(2026, 3, 1), cronograma[0].Competencia);
        Assert.Equal(new DateOnly(2026, 4, 1), cronograma[1].Competencia);
        Assert.Equal(new DateOnly(2026, 5, 1), cronograma[2].Competencia);
        Assert.Equal(new DateOnly(2026, 6, 1), cronograma[3].Competencia);
    }

    [Fact]
    public void Gerar_meses_menor_que_1_lanca()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CronogramaLinear.Gerar(1000, 0, new DateOnly(2026, 1, 1)));
}
