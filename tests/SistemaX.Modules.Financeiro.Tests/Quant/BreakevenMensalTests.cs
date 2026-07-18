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

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // Ideia 1 do matemonstro (docs/financeiro/ideias-matemonstro.md) — margem de segurança, GAO e
    // PE econômico: 3 campos derivados do mesmo cálculo, sem insumo novo.
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Margem_de_seguranca_e_zero_quando_receita_atual_bate_exatamente_no_breakeven()
    {
        // fixos=1000, MC=50% -> receita necessária=2000; receita acumulada=2000 (bate exato) -> MS=0.
        var receitas = new[]
        {
            new BreakevenMensal.PontoReceitaDiaria(1, 1_000_00),
            new BreakevenMensal.PontoReceitaDiaria(2, 1_000_00),
        };

        var resultado = BreakevenMensal.Calcular(1_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.Equal(2_000_00, resultado.ReceitaAcumuladaCentavos);
        Assert.Equal(2_000_00, resultado.ReceitaNecessariaMensalCentavos);
        Assert.NotNull(resultado.MargemDeSegurancaPercentual);
        Assert.Equal(0.0, resultado.MargemDeSegurancaPercentual!.Value, precision: 10);
    }

    [Fact]
    public void Margem_de_seguranca_positiva_quando_receita_atual_supera_o_necessario()
    {
        // fixos=1000, MC=50% -> necessário=2000; acumulado=4000 -> MS=(4000-2000)/4000=50%.
        var receitas = new[] { new BreakevenMensal.PontoReceitaDiaria(1, 4_000_00) };
        var resultado = BreakevenMensal.Calcular(1_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.NotNull(resultado.MargemDeSegurancaPercentual);
        Assert.Equal(0.50, resultado.MargemDeSegurancaPercentual!.Value, precision: 10);
    }

    [Fact]
    public void Margem_de_seguranca_e_nula_sem_receita_acumulada_ou_sem_margem_de_contribuicao()
    {
        var semReceita = BreakevenMensal.Calcular(1_000_00, 0.50, [], diasNoMes: 30);
        Assert.Null(semReceita.MargemDeSegurancaPercentual);

        var semMc = BreakevenMensal.Calcular(1_000_00, 0, [new BreakevenMensal.PontoReceitaDiaria(1, 1_000_00)], diasNoMes: 30);
        Assert.Null(semMc.MargemDeSegurancaPercentual);
    }

    [Fact]
    public void Gao_e_a_razao_entre_mc_acumulada_e_lucro_operacional_quando_lucro_e_positivo()
    {
        // fixos=1000, MC=50% de 4000=2000 acumulada -> lucro_op=2000-1000=1000 -> GAO=2000/1000=2.
        var receitas = new[] { new BreakevenMensal.PontoReceitaDiaria(1, 4_000_00) };
        var resultado = BreakevenMensal.Calcular(1_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.NotNull(resultado.Gao);
        Assert.Equal(2.0, resultado.Gao!.Value, precision: 10);
    }

    [Fact]
    public void Gao_e_nulo_quando_lucro_operacional_nao_e_positivo()
    {
        // MC acumulada (500) não cobre os fixos (1000) -> lucro_op <= 0 -> GAO indefinido.
        var receitas = new[] { new BreakevenMensal.PontoReceitaDiaria(1, 1_000_00) };
        var resultado = BreakevenMensal.Calcular(1_000_00, 0.50, receitas, diasNoMes: 30);

        Assert.Null(resultado.Gao);
    }

    [Fact]
    public void Pe_economico_e_igual_ao_pe_contabil_quando_custo_de_oportunidade_e_zero()
    {
        var resultado = BreakevenMensal.Calcular(
            custosFixosMensaisCentavos: 10_000_00, margemContribuicaoPercentual: 0.40,
            receitaDiariaDoMesOrdenadaPorDia: [], diasNoMes: 30, custoDeOportunidadeMensalCentavos: 0);

        Assert.Equal(resultado.ReceitaNecessariaMensalCentavos, resultado.ReceitaNecessariaMensalEconomicaCentavos);
    }

    [Fact]
    public void Pe_economico_soma_o_custo_de_oportunidade_aos_custos_fixos_antes_de_dividir_pela_mc()
    {
        // (fixos=10.000 + oportunidade=2.000) / MC 40% = 30.000 -> maior que o PE contábil (25.000).
        var resultado = BreakevenMensal.Calcular(
            custosFixosMensaisCentavos: 10_000_00, margemContribuicaoPercentual: 0.40,
            receitaDiariaDoMesOrdenadaPorDia: [], diasNoMes: 30, custoDeOportunidadeMensalCentavos: 2_000_00);

        Assert.Equal(25_000_00, resultado.ReceitaNecessariaMensalCentavos);
        Assert.Equal(30_000_00, resultado.ReceitaNecessariaMensalEconomicaCentavos);
        Assert.True(resultado.ReceitaNecessariaMensalEconomicaCentavos > resultado.ReceitaNecessariaMensalCentavos);
    }
}
