using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// Caso de sanidade DigiSat (docs/financeiro/design-analise-por-projeto.md §4.3): 7×R$985
/// (R$6.895) → intangível, vida 36m → R$191,53/mês (28 meses) + R$191,52/mês (8 meses),
/// Σ = R$6.895,00 exato (Hamilton — nenhum centavo perdido).
/// </summary>
public sealed class AtivoDeCapitalQuantTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static AtivoDeCapital CriarDigiSat()
        => AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 36, Agora,
            quantidadeUnidades: 5, projetoId: "projeto-digisat").Valor;

    [Fact]
    public void Cronograma_DigiSat_28MesesA19153_8MesesA19152_SomaExatamenteOTotal()
    {
        var ativo = CriarDigiSat();
        var cronograma = AtivoDeCapitalQuant.Cronograma(ativo);

        Assert.Equal(36, cronograma.Count);
        Assert.Equal(28, cronograma.Count(c => c.ValorCentavos == 19_153));
        Assert.Equal(8, cronograma.Count(c => c.ValorCentavos == 19_152));
        Assert.Equal(689_500, cronograma.Sum(c => c.ValorCentavos));

        // Hamilton: o centavo extra vai às PRIMEIRAS competências (desempate ThenBy índice).
        Assert.Equal(19_153, cronograma[0].ValorCentavos);
        Assert.Equal(19_152, cronograma[35].ValorCentavos);
    }

    [Fact]
    public void ValorNaCompetencia_PrimeiroMes_E19153Centavos()
    {
        var ativo = CriarDigiSat();
        Assert.Equal(19_153, AtivoDeCapitalQuant.ValorNaCompetencia(ativo, new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public void SomaNaJanela_MesUnico_RetornaOValorDoMes()
    {
        var ativo = CriarDigiSat();
        var soma = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));
        Assert.Equal(19_153, soma);
    }

    [Fact]
    public void SomaNaJanela_VidaInteira_SomaExatamenteOCusto()
    {
        var ativo = CriarDigiSat();
        var soma = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 7, 1), new DateOnly(2029, 6, 30));
        Assert.Equal(689_500, soma);
    }

    [Fact]
    public void CustoPorLicenca_HamiltonCincoUnidades_R1379Cada()
    {
        // Alocar(689.500, [1,1,1,1,1]) = 5 × 137.900 (design §4.3).
        var pesos = Enumerable.Repeat(1L, 5).ToList();
        var alocado = SistemaX.Modules.Financeiro.Application.Quant.RateioProporcional.Alocar(689_500, pesos);

        Assert.All(alocado, v => Assert.Equal(137_900, v));
        Assert.Equal(689_500, alocado.Sum());
    }

    [Fact]
    public void SomaNaJanela_AposBaixa_ParaDeReconhecerENaoDuplica()
    {
        var ativo = CriarDigiSat();
        ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 19_153, Agora); // mês 1: 19.153 reconhecido
        var recognizedSoFar = AtivoDeCapitalQuant.ReconhecidoAteOCursor(ativo);
        Assert.Equal(19_153, recognizedSoFar);

        var valorContabil = ativo.CustoAquisicao.Centavos - recognizedSoFar; // 670.347
        ativo.Baixar("Contrato encerrado", new DateOnly(2026, 8, 1), valorContabil, Agora);

        // Mês da baixa reconhece o RESTANTE de uma vez — não a fatia linear (19.153).
        var somaMesBaixa = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        Assert.Equal(670_347, somaMesBaixa);

        // Nenhuma competência POSTERIOR reconhece nada.
        var somaDepois = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 9, 1), new DateOnly(2029, 6, 30));
        Assert.Equal(0, somaDepois);

        // Σ vida inteira (mês 1 + baixa) = custo total exato — invariante "baixa reconhece o resto exato".
        var somaTotal = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 7, 1), new DateOnly(2029, 6, 30));
        Assert.Equal(689_500, somaTotal);
        Assert.Equal(0, AtivoDeCapitalQuant.ValorContabilAtualCentavos(ativo));
    }
}
