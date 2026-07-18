using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// IMOBILIZADO TANGÍVEL REUSA O MESMO AGREGADO/CRONOGRAMA (docs/financeiro/design-imobilizado-roi.md
/// §1/§4.3) — os 5 bens da abertura da assistência (Reforma/Equipamento/Placa/Computador/Móveis),
/// natureza <see cref="NaturezaAtivo.Tangivel"/>, passando pela MESMA <c>AtivoDeCapitalQuant</c>/
/// <c>Quant.CronogramaLinear</c> que já cobre o caso DigiSat (intangível). Nenhuma máquina paralela.
/// </summary>
public sealed class ImobilizadoTangivelTests
{
    private const string Biz = "assistencia-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static AtivoDeCapital Criar(string nome, CategoriaAtivo categoria, decimal custoReais, int vidaUtilMeses)
        => AtivoDeCapital.Criar(
            Biz, nome, NaturezaAtivo.Tangivel, categoria, Money.DeReais(custoReais), Money.Zero,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), vidaUtilMeses, Agora).Valor;

    [Fact]
    public void Reforma_R25000_60Meses_DeprecicaoMensal41667ComRestoNasPrimeiras()
    {
        var ativo = Criar("Reforma da loja", CategoriaAtivo.Reforma, 25_000, 60);
        var cronograma = AtivoDeCapitalQuant.Cronograma(ativo);

        Assert.Equal(60, cronograma.Count);
        Assert.Equal(40, cronograma.Count(c => c.ValorCentavos == 41_667));
        Assert.Equal(20, cronograma.Count(c => c.ValorCentavos == 41_666));
        Assert.Equal(2_500_000, cronograma.Sum(c => c.ValorCentavos)); // Σ = custo exato (Hamilton)
    }

    [Fact]
    public void Equipamento_R12000_60Meses_DepreciacaoMensalExata20000()
    {
        var ativo = Criar("Bancada ESD", CategoriaAtivo.Equipamento, 12_000, 60);
        Assert.Equal(20_000, AtivoDeCapitalQuant.ValorNaCompetencia(ativo, new DateOnly(2026, 7, 1)));
        Assert.Equal(1_200_000, AtivoDeCapitalQuant.Cronograma(ativo).Sum(c => c.ValorCentavos));
    }

    [Fact]
    public void Placa_R4800_48Meses_DepreciacaoMensalExata10000()
    {
        var ativo = Criar("Comunicação visual", CategoriaAtivo.ComunicacaoVisual, 4_800, 48);
        Assert.Equal(10_000, AtivoDeCapitalQuant.ValorNaCompetencia(ativo, new DateOnly(2026, 7, 1)));
        Assert.Equal(480_000, AtivoDeCapitalQuant.Cronograma(ativo).Sum(c => c.ValorCentavos));
    }

    [Fact]
    public void Computador_R6000_60Meses_DepreciacaoMensalExata10000()
    {
        var ativo = Criar("Computador", CategoriaAtivo.Computador, 6_000, 60);
        Assert.Equal(10_000, AtivoDeCapitalQuant.ValorNaCompetencia(ativo, new DateOnly(2026, 7, 1)));
        Assert.Equal(600_000, AtivoDeCapitalQuant.Cronograma(ativo).Sum(c => c.ValorCentavos));
    }

    [Fact]
    public void Moveis_R8000_120Meses_DepreciacaoMensal6667ComRestoNasPrimeiras()
    {
        var ativo = Criar("Móveis", CategoriaAtivo.Moveis, 8_000, 120);
        var cronograma = AtivoDeCapitalQuant.Cronograma(ativo);

        Assert.Equal(120, cronograma.Count);
        Assert.Equal(80, cronograma.Count(c => c.ValorCentavos == 6_667));
        Assert.Equal(40, cronograma.Count(c => c.ValorCentavos == 6_666));
        Assert.Equal(800_000, cronograma.Sum(c => c.ValorCentavos));
    }

    /// <summary>§4.3 — o total de D&A do MÊS 1 da abertura, somando os 5 bens: R$883,34.</summary>
    [Fact]
    public void TodosOsBensDaAbertura_DepreciacaoTotalDoMes1EhR883E34()
    {
        var reforma = Criar("Reforma", CategoriaAtivo.Reforma, 25_000, 60);
        var equipamento = Criar("Equipamento", CategoriaAtivo.Equipamento, 12_000, 60);
        var placa = Criar("Placa", CategoriaAtivo.ComunicacaoVisual, 4_800, 48);
        var computador = Criar("Computador", CategoriaAtivo.Computador, 6_000, 60);
        var moveis = Criar("Móveis", CategoriaAtivo.Moveis, 8_000, 120);

        var totalMes1 = new[] { reforma, equipamento, placa, computador, moveis }
            .Sum(a => AtivoDeCapitalQuant.ValorNaCompetencia(a, new DateOnly(2026, 7, 1)));

        Assert.Equal(88_334, totalMes1); // R$883,34 em centavos
    }

    [Fact]
    public void ValorContabilAtual_ConvergeParaZeroAoFimDaVidaUtil_ParaTangivelResidualZero()
    {
        var ativo = Criar("Computador", CategoriaAtivo.Computador, 6_000, 60);
        foreach (var (competencia, valor) in AtivoDeCapitalQuant.Cronograma(ativo))
        {
            ativo.ReconhecerCompetencia(competencia, valor, Agora);
        }

        Assert.Equal(0, AtivoDeCapitalQuant.ValorContabilAtualCentavos(ativo));
        Assert.Equal(StatusAtivoDeCapital.Encerrado, ativo.Status);
    }
}
