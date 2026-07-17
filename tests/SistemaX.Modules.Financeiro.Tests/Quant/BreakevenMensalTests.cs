using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class BreakevenMensalTests
{
    [Fact]
    public void Receita_necessaria_mensal_e_custos_fixos_dividido_pela_margem()
    {
        var resultado = BreakevenMensal.Calcular(
            custosFixosMensaisCentavos: 10_000_00,
            margemContribuicaoPercentual: 0.40,
            receitaDiariaDoMesOrdenadaPorDia: [],
            diasNoMes: 30);

        Assert.Equal(25_000_00, resultado.ReceitaNecessariaMensalCentavos);
        Assert.Equal((long)Math.Ceiling(25_000_00 / 30.0), resultado.ReceitaNecessariaDiariaCentavos);
    }

    [Fact]
    public void Dia_do_equilibrio_e_o_primeiro_dia_em_que_o_acumulado_de_mc_bate_os_fixos()
    {
        // fixos=1000, MC=50% -> precisa de MC acumulada=1000 -> receita acumulada=2000
        var receitas = new[]
        {
            new BreakevenMensal.PontoReceitaDiaria(1, 500_00),
            new BreakevenMensal.PontoReceitaDiaria(2, 500_00),
            new BreakevenMensal.PontoReceitaDiaria(3, 500_00),
            new BreakevenMensal.PontoReceitaDiaria(4, 500_00),
        };

        var resultado = BreakevenMensal.Calcular(1_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.Equal(4, resultado.DiaDoEquilibrio);
        Assert.True(resultado.JaAtingiuNoMes);
    }

    [Fact]
    public void Sem_bater_nos_dias_decorridos_projeta_linearmente_pelo_ritmo_medio_do_mes()
    {
        // 10 dias decorridos, MC acumulada = 10*100=1000 (média 100/dia); faltam 4000 -> +40 dias -> dia 50, além do mês (30) -> null
        var receitas = Enumerable.Range(1, 10).Select(d => new BreakevenMensal.PontoReceitaDiaria(d, 200_00)).ToList();
        var resultado = BreakevenMensal.Calcular(5_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.Null(resultado.DiaDoEquilibrio);
        Assert.False(resultado.JaAtingiuNoMes);
    }

    [Fact]
    public void Projecao_linear_bate_dentro_do_mes_quando_o_ritmo_permite()
    {
        // 5 dias decorridos, MC=50% de 400/dia = 200/dia acumulado=1000; faltam 500 -> +3 dias -> dia 8 (<=30)
        var receitas = Enumerable.Range(1, 5).Select(d => new BreakevenMensal.PontoReceitaDiaria(d, 400_00)).ToList();
        var resultado = BreakevenMensal.Calcular(1_500_00, 0.50, receitas, diasNoMes: 30);

        Assert.Equal(8, resultado.DiaDoEquilibrio);
        Assert.False(resultado.JaAtingiuNoMes); // dia projetado (8) é maior que os dias decorridos (5)
    }

    [Fact]
    public void Margem_de_contribuicao_zero_nunca_atinge_equilibrio()
    {
        var receitas = new[] { new BreakevenMensal.PontoReceitaDiaria(1, 100_000_00) };
        var resultado = BreakevenMensal.Calcular(1_000_00, 0, receitas, diasNoMes: 30);

        Assert.Null(resultado.DiaDoEquilibrio);
        Assert.Equal(long.MaxValue, resultado.ReceitaNecessariaMensalCentavos);
    }

    [Fact]
    public void Dias_no_mes_invalido_lanca_excecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BreakevenMensal.Calcular(100, 0.5, [], 0));
    }
}
