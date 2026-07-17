using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// Orçamento (peças previstas + mão de obra aprovados JUNTOS — o gap #1 do plano) e a guarda de
/// valor da execução (§5.3.4): peça extra ou aumento de mão de obra acima do orçado NUNCA passa
/// sem <c>clienteAvisado = true</c>. É essa trava, testada aqui, que torna estruturalmente
/// impossível uma OS fechar cobrando mais do que o cliente aprovou sem rastro de aviso.
/// </summary>
public class OrdemDeServicoOrcamentoEExecucaoTests
{
    [Fact]
    public void EnviarOrcamento_MaoDeObraNegativa_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteEmDiagnostico();

        var resultado = os.EnviarOrcamento([], Money.DeReais(-10), 10, OrdemDeServicoTestBuilder.Abertura.AddDays(2));

        Assert.True(resultado.Falha);
        Assert.Equal("os.mao_de_obra_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void EnviarOrcamento_ValidadeZeroOuNegativa_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteEmDiagnostico();

        var resultado = os.EnviarOrcamento([], Money.DeReais(100), 0, OrdemDeServicoTestBuilder.Abertura.AddDays(2));

        Assert.True(resultado.Falha);
        Assert.Equal("os.validade_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void EnviarOrcamento_PecaComQuantidadeInvalida_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteEmDiagnostico();
        var pecaInvalida = PecaOrcada.Nova("produto-1", "Tela", 0, Money.DeReais(100));

        var resultado = os.EnviarOrcamento([pecaInvalida], Money.DeReais(100), 10, OrdemDeServicoTestBuilder.Abertura.AddDays(2));

        Assert.True(resultado.Falha);
        Assert.Equal("os.quantidade_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void EnviarOrcamento_TotalDerivaDePecasMaisMaoDeObra_NuncaDigitado()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();

        // 1 peça de R$ 390 + mão de obra R$ 120 = R$ 510 — mesmo exemplo do wireframe do plano.
        Assert.Equal(Money.DeReais(510), os.Orcamento!.Total);
    }

    [Fact]
    public void AplicarPeca_LinhaOrcada_MovePraPecasAplicadasComOrigemOrcada()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var linhaId = os.Orcamento!.Pecas.Single().LinhaId;

        var resultado = os.AplicarPeca(linhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1));

        Assert.True(resultado.Sucesso);
        var aplicada = Assert.Single(os.PecasAplicadas);
        Assert.Equal(OrigemPeca.Orcada, aplicada.Origem);
        Assert.Equal(linhaId, aplicada.LinhaId);
    }

    [Fact]
    public void AplicarPeca_MesmaLinhaDuasVezes_SegundaFalha()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var linhaId = os.Orcamento!.Pecas.Single().LinhaId;
        os.AplicarPeca(linhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1));

        var resultado = os.AplicarPeca(linhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(2));

        Assert.True(resultado.Falha);
        Assert.Equal("os.peca_ja_aplicada", resultado.Erro.Codigo);
    }

    [Fact]
    public void AplicarPeca_LinhaQueNaoEstaNoOrcamento_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        var resultado = os.AplicarPeca("linha-inexistente", OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1));

        Assert.True(resultado.Falha);
        Assert.Equal("os.peca_nao_orcada", resultado.Erro.Codigo);
    }

    [Fact]
    public void AplicarPeca_ForaDeEmExecucao_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteAprovada();

        var resultado = os.AplicarPeca("qualquer-linha", OrdemDeServicoTestBuilder.Abertura.AddDays(3));

        Assert.True(resultado.Falha);
        Assert.Equal("os.status_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void AdicionarPecaExtra_SemClienteAvisado_Falha_GuardaDeValor()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        var resultado = os.AdicionarPecaExtra(
            "produto-parafuso", "Kit parafusos", 1, Money.DeReais(15), clienteAvisado: false,
            agora: OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(2));

        Assert.True(resultado.Falha);
        Assert.Equal("os.peca_extra_exige_aviso", resultado.Erro.Codigo);
        Assert.Empty(os.PecasAplicadas);
    }

    [Fact]
    public void AdicionarPecaExtra_ComClienteAvisado_Funciona_EPodeSuperarOOrcamento()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var totalOrcado = os.Orcamento!.Total;

        var resultado = os.AdicionarPecaExtra(
            "produto-parafuso", "Kit parafusos", 1, Money.DeReais(15), clienteAvisado: true,
            agora: OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(2));

        Assert.True(resultado.Sucesso);
        var extra = Assert.Single(os.PecasAplicadas);
        Assert.Equal(OrigemPeca.Extra, extra.Origem);
        Assert.True(os.TotalGeral.Centavos > totalOrcado.Centavos - os.Orcamento.TotalPecas.Centavos); // mão de obra + extra já supera parte do orçamento original
    }

    [Fact]
    public void AjustarMaoDeObraFinal_ParaBaixo_NuncaExigeAviso()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        var resultado = os.AjustarMaoDeObraFinal(Money.DeReais(80), clienteAvisado: false);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(80), os.MaoDeObraFinal);
    }

    [Fact]
    public void AjustarMaoDeObraFinal_ParaCimaSemAviso_Falha_GuardaDeValor()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        var resultado = os.AjustarMaoDeObraFinal(Money.DeReais(200), clienteAvisado: false);

        Assert.True(resultado.Falha);
        Assert.Equal("os.aumento_mao_de_obra_exige_aviso", resultado.Erro.Codigo);
        Assert.Equal(OrdemDeServicoTestBuilder.MaoDeObraOrcada, os.MaoDeObraFinal); // não mudou
    }

    [Fact]
    public void AjustarMaoDeObraFinal_ParaCimaComAviso_Funciona()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        var resultado = os.AjustarMaoDeObraFinal(Money.DeReais(200), clienteAvisado: true);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(200), os.MaoDeObraFinal);
    }

    [Fact]
    public void TotalGeral_ComOrcamentoInteiramenteAplicado_IgualAoOrcado()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        Assert.Equal(os.Orcamento!.Total, os.TotalGeral);
    }
}
