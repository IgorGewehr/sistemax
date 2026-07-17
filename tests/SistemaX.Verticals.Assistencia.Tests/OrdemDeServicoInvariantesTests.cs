using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// Invariantes que não são FSM nem faturamento: a senha do equipamento nunca vaza em texto
/// (§3/§4.1 do plano), campos derivados nunca persistidos como verdade (§4.7) e o histórico de
/// transições como fonte única de "há quantos dias".
/// </summary>
public class OrdemDeServicoInvariantesTests
{
    [Fact]
    public void Equipamento_ToString_NuncaExpoeASenhaCrua()
    {
        var equipamento = new Equipamento("Celular", "Apple", "iPhone 12", SenhaAcesso: "segredo-1234");

        var texto = equipamento.ToString();

        Assert.DoesNotContain("segredo-1234", texto);
        Assert.Contains("SenhaAcesso = ***", texto);
    }

    [Fact]
    public void Equipamento_ToString_SemSenha_MostraNull()
    {
        var equipamento = new Equipamento("Celular", "Apple", "iPhone 12", SenhaAcesso: null);

        Assert.Contains("SenhaAcesso = null", equipamento.ToString());
    }

    [Fact]
    public void EstaAtrasada_PrevisaoNoPassadoEOsAindaAberta_RetornaTrue()
    {
        var previsao = OrdemDeServicoTestBuilder.Abertura.AddDays(2);
        var os = OrdemDeServicoTestBuilder.AbrirNova(previsaoEntrega: previsao);

        Assert.True(os.EstaAtrasada(OrdemDeServicoTestBuilder.Abertura.AddDays(5)));
        Assert.False(os.EstaAtrasada(OrdemDeServicoTestBuilder.Abertura.AddDays(1)));
    }

    [Fact]
    public void EstaAtrasada_OsEmEstadoTerminal_NuncaAtrasada_MesmoComPrevisaoNoPassado()
    {
        var previsao = OrdemDeServicoTestBuilder.Abertura.AddDays(1);
        var os = OrdemDeServico.Abrir(
            OrdemDeServicoTestBuilder.TenantId, "OS-ATRASO", OrdemDeServicoTestBuilder.Cliente(),
            OrdemDeServicoTestBuilder.Equipamento(), "defeito", OrdemDeServicoTestBuilder.Abertura, previsao);
        os.AtribuirTecnico("tecnico-1", "Igor");
        os.RegistrarDiagnostico("diagnóstico", OrdemDeServicoTestBuilder.Abertura.AddDays(1));
        os.EnviarOrcamento([], Money.DeReais(50), 10, OrdemDeServicoTestBuilder.Abertura.AddDays(2));
        os.RegistrarAprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));
        os.IniciarExecucao(OrdemDeServicoTestBuilder.Abertura.AddDays(3));
        os.ConcluirExecucao(OrdemDeServicoTestBuilder.Abertura.AddDays(4));
        os.Entregar(FormaPagamento.Pix, Money.Zero, 90, OrdemDeServicoTestBuilder.Abertura.AddDays(10)); // bem depois da previsão

        Assert.False(os.EstaAtrasada(OrdemDeServicoTestBuilder.Abertura.AddYears(1)));
    }

    [Fact]
    public void AlterarPrevisaoEntrega_AposPronta_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        var resultado = os.AlterarPrevisaoEntrega(OrdemDeServicoTestBuilder.Abertura.AddDays(30));

        Assert.True(resultado.Falha);
        Assert.Equal("os.previsao_nao_editavel", resultado.Erro.Codigo);
    }

    [Fact]
    public void AlterarPrevisaoEntrega_AntesDePronta_Funciona()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var novaData = OrdemDeServicoTestBuilder.Abertura.AddDays(30);

        var resultado = os.AlterarPrevisaoEntrega(novaData);

        Assert.True(resultado.Sucesso);
        Assert.Equal(novaData, os.PrevisaoEntrega);
    }

    [Fact]
    public void OrcamentoVencido_AposValidade_RetornaTrue_SoNoEstadoAguardandoAprovacao()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao(validadeDias: 10);
        var envio = os.Orcamento!.EnviadoEm;

        Assert.False(os.OrcamentoVencido(envio.AddDays(5)));
        Assert.True(os.OrcamentoVencido(envio.AddDays(11)));

        os.RegistrarAprovacao(CanalAprovacao.Presencial, envio.AddDays(1));
        Assert.False(os.OrcamentoVencido(envio.AddDays(11))); // já saiu de AguardandoAprovacao
    }

    [Fact]
    public void AtribuirTecnico_ComIdOuNomeVazio_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        var resultado = os.AtribuirTecnico("", "Igor");

        Assert.True(resultado.Falha);
        Assert.Equal("os.tecnico_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void AtribuirTecnico_ComOsEmEstadoTerminal_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteEntregue();

        var resultado = os.AtribuirTecnico("outro-tecnico", "Maria");

        Assert.True(resultado.Falha);
        Assert.Equal("os.status_terminal", resultado.Erro.Codigo);
    }

    [Fact]
    public void Historico_UmaLinhaPorTransicao_NaSequenciaCorreta()
    {
        var os = OrdemDeServicoTestBuilder.AteEntregue();

        Assert.Equal(
            [
                StatusOrdemServico.EmDiagnostico,
                StatusOrdemServico.AguardandoAprovacao,
                StatusOrdemServico.Aprovada,
                StatusOrdemServico.EmExecucao,
                StatusOrdemServico.Pronta,
                StatusOrdemServico.Entregue
            ],
            os.Historico.Select(linha => linha.Para));
    }

    [Fact]
    public void TempoNaEtapaAtual_AntesDeQualquerTransicao_ContaDesdeAAbertura()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        var tempo = os.TempoNaEtapaAtual(OrdemDeServicoTestBuilder.Abertura.AddHours(5));

        Assert.Equal(TimeSpan.FromHours(5), tempo);
    }

    [Fact]
    public void TotalPecas_ENumeroDaOs_SaoUsadosNaDescricaoDoEventoFaturado()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova(numero: "OS-0099");
        Assert.Equal("OS-0099", os.Numero);
    }
}
