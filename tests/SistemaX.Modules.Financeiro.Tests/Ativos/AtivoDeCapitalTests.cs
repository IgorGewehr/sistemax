using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// Domínio de <see cref="AtivoDeCapital"/> — invariantes de <c>Criar</c>, cursor de competência e
/// FSM. O caso DigiSat (docs/financeiro/design-analise-por-projeto.md §4.3: R$6.895 ÷ 36 meses)
/// entra via <c>AtivoDeCapitalQuant</c> (Application) — aqui só o agregado puro.
/// </summary>
public sealed class AtivoDeCapitalTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Criar_ComValorResidualMaiorOuIgualAoCusto_Falha()
    {
        var resultado = AtivoDeCapital.Criar(
            Biz, "Bem", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(1000), Money.DeReais(1000), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 12, Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.residual_maior_que_custo", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComVidaUtilZero_Falha()
    {
        var resultado = AtivoDeCapital.Criar(
            Biz, "Bem", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(1000), Money.Zero, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), 0, Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.vida_util_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComInicioDepreciacaoAntesDaAquisicao_Falha()
    {
        var resultado = AtivoDeCapital.Criar(
            Biz, "Bem", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(1000), Money.Zero, new DateOnly(2026, 3, 1), new DateOnly(2026, 1, 1), 12, Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.inicio_antes_da_aquisicao", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_DigiSat_NasceEmUsoComCamposCorretos()
    {
        var ativo = CriarDigiSat();

        Assert.Equal(StatusAtivoDeCapital.EmUso, ativo.Status);
        Assert.Equal(NaturezaAtivo.Intangivel, ativo.Natureza);
        Assert.Equal(5, ativo.QuantidadeUnidades);
        Assert.Equal(689_500, ativo.CustoAquisicao.Centavos);
        Assert.Equal(689_500, ativo.BaseDepreciavel.Centavos); // residual 0
        Assert.Equal(new DateOnly(2026, 7, 1), ativo.ProximaCompetenciaDevida);
    }

    [Fact]
    public void ReconhecerCompetencia_ComCompetenciaDiferenteDaDevida_Falha()
    {
        var ativo = CriarDigiSat();
        var resultado = ativo.ReconhecerCompetencia(new DateOnly(2026, 8, 1), 19_153, Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.competencia_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void ReconhecerCompetencia_AvancaCursorEPermanaceEmUsoAntesDaUltima()
    {
        var ativo = CriarDigiSat();
        var resultado = ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 19_153, Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.EmUso, ativo.Status);
        Assert.Equal(new DateOnly(2026, 8, 1), ativo.ProximaCompetenciaDevida);
    }

    [Fact]
    public void ReconhecerCompetencia_NaUltimaCompetencia_TransicionaParaEncerrado()
    {
        var ativo = CriarDigiSat();
        var competencia = new DateOnly(2026, 7, 1);
        for (var i = 0; i < 35; i++)
        {
            var r = ativo.ReconhecerCompetencia(competencia, 19_153, Agora);
            Assert.True(r.Sucesso);
            competencia = competencia.AddMonths(1);
            Assert.Equal(StatusAtivoDeCapital.EmUso, ativo.Status);
        }

        var ultimo = ativo.ReconhecerCompetencia(competencia, 19_152, Agora);
        Assert.True(ultimo.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Encerrado, ativo.Status);
        Assert.NotNull(ativo.EncerradoEm);
    }

    [Fact]
    public void ReconhecerCompetencia_AtivoJaEncerrado_Falha()
    {
        var ativo = CriarDigiSat();
        var competencia = new DateOnly(2026, 7, 1);
        for (var i = 0; i < 36; i++)
        {
            var valor = i == 35 ? 19_152 : 19_153;
            ativo.ReconhecerCompetencia(competencia, valor, Agora);
            competencia = competencia.AddMonths(1);
        }
        Assert.Equal(StatusAtivoDeCapital.Encerrado, ativo.Status);

        var resultado = ativo.ReconhecerCompetencia(competencia, 100, Agora);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.nao_em_uso", resultado.Erro.Codigo);
    }

    [Fact]
    public void Baixar_ReconheceValorContabilEZeraAValorContabilFuturo()
    {
        var ativo = CriarDigiSat();
        ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 19_153, Agora); // mês 1 reconhecido

        var resultado = ativo.Baixar("Contrato encerrado", new DateOnly(2026, 8, 1), 670_347, Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Baixado, ativo.Status);
        Assert.Equal(670_347, ativo.ValorReconhecidoNaBaixaCentavos);
        Assert.Equal("Contrato encerrado", ativo.MotivoBaixa);
    }

    [Fact]
    public void Baixar_ComCompetenciaAnteriorAoCursor_Falha()
    {
        var ativo = CriarDigiSat();
        ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 19_153, Agora);

        var resultado = ativo.Baixar("Motivo", new DateOnly(2026, 6, 1), 100, Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.baixa_competencia_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Baixar_EmUsoOuEncerrado_AmbosPermitidos_MasBaixadoDeNovoFalha()
    {
        var ativo = CriarDigiSat();
        Assert.True(ativo.Baixar("Motivo 1", new DateOnly(2026, 7, 1), 689_500, Agora).Sucesso);

        var segunda = ativo.Baixar("Motivo 2", new DateOnly(2026, 8, 1), 0, Agora);
        Assert.True(segunda.Falha);
        Assert.Equal("financeiro.ativo.transicao_invalida", segunda.Erro.Codigo);
    }

    private static AtivoDeCapital CriarDigiSat()
        => AtivoDeCapital.Criar(
            Biz, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 36, Agora,
            quantidadeUnidades: 5, projetoId: "projeto-digisat").Valor;
}
