using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// FSM de <see cref="OrdemDeServico"/> (R4 do projeto): cobre toda transição válida E toda
/// transição inválida — nunca só o caminho feliz. Também cobre os dois deltas cirúrgicos do
/// plano em relação ao esqueleto original: <c>Reprovada</c> deixou de ser terminal (ganhou
/// <c>DevolverSemReparo</c>) e <c>AguardandoAprovacao</c> tem auto-loop (reenvio de orçamento).
/// </summary>
public class OrdemDeServicoFsmTests
{
    [Fact]
    public void Abrir_ComecaNoStatusAberta()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        Assert.Equal(StatusOrdemServico.Aberta, os.Status);
        Assert.Empty(os.Historico);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Abrir_TenantIdVazio_LancaArgumentException(string tenantId)
    {
        Assert.Throws<ArgumentException>(() => OrdemDeServico.Abrir(
            tenantId, "OS-0001", OrdemDeServicoTestBuilder.Cliente(), OrdemDeServicoTestBuilder.Equipamento(),
            "defeito", OrdemDeServicoTestBuilder.Abertura));
    }

    [Fact]
    public void Abrir_DefeitoRelatadoVazio_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => OrdemDeServico.Abrir(
            OrdemDeServicoTestBuilder.TenantId, "OS-0001", OrdemDeServicoTestBuilder.Cliente(),
            OrdemDeServicoTestBuilder.Equipamento(), "  ", OrdemDeServicoTestBuilder.Abertura));
    }

    [Fact]
    public void RegistrarDiagnostico_SemTecnicoAtribuido_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        var resultado = os.RegistrarDiagnostico("Pasta térmica ressecada.", OrdemDeServicoTestBuilder.Abertura.AddDays(1));

        Assert.True(resultado.Falha);
        Assert.Equal("os.tecnico_obrigatorio", resultado.Erro.Codigo);
        Assert.Equal(StatusOrdemServico.Aberta, os.Status);
    }

    [Fact]
    public void RegistrarDiagnostico_DiagnosticoVazio_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();
        os.AtribuirTecnico("tecnico-1", "Igor");

        var resultado = os.RegistrarDiagnostico("   ", OrdemDeServicoTestBuilder.Abertura.AddDays(1));

        Assert.True(resultado.Falha);
        Assert.Equal("os.diagnostico_obrigatorio", resultado.Erro.Codigo);
    }

    [Fact]
    public void RegistrarDiagnostico_ComTecnicoEDiagnostico_TransitaERegistraHistorico()
    {
        var os = OrdemDeServicoTestBuilder.AteEmDiagnostico();

        Assert.Equal(StatusOrdemServico.EmDiagnostico, os.Status);
        var linha = Assert.Single(os.Historico);
        Assert.Equal(StatusOrdemServico.Aberta, linha.De);
        Assert.Equal(StatusOrdemServico.EmDiagnostico, linha.Para);
    }

    [Fact]
    public void EnviarOrcamento_ReenvioNoMesmoEstado_SubstituiOAnteriorSemNovaLinhaDeHistorico()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();
        var historicoAntes = os.Historico.Count;

        var novaPeca = PecaOrcada.Nova("produto-2", "Tela", 1, Money.DeReais(500));
        var resultado = os.EnviarOrcamento([novaPeca], Money.DeReais(200), 15, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(5));

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusOrdemServico.AguardandoAprovacao, os.Status);
        Assert.Equal(historicoAntes, os.Historico.Count); // reenvio não é transição nova
        Assert.Equal(Money.DeReais(200), os.Orcamento!.MaoDeObra);
        Assert.Single(os.Orcamento.Pecas);
        Assert.Equal("produto-2", os.Orcamento.Pecas[0].ProdutoId);
    }

    [Fact]
    public void RegistrarReprovacao_TransitaParaReprovada_NaoEhMaisTerminal()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();

        var resultado = os.RegistrarReprovacao(CanalAprovacao.Telefone, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1), "Achou caro");

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusOrdemServico.Reprovada, os.Status);
        Assert.Equal("Achou caro", os.MotivoReprovacao);
        Assert.Equal(DecisaoOrcamento.Reprovada, os.Aprovacao!.Decisao);

        // delta do plano: Reprovada deixou de ser terminal
        var devolucao = os.DevolverSemReparo(Money.Zero, OrdemDeServicoTestBuilder.Abertura.AddDays(3));
        Assert.True(devolucao.Sucesso);
        Assert.Equal(StatusOrdemServico.DevolvidaSemReparo, os.Status);
    }

    [Fact]
    public void DevolverSemReparo_ForaDeReprovada_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        var resultado = os.DevolverSemReparo(Money.Zero, OrdemDeServicoTestBuilder.Abertura.AddDays(1));

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void IniciarExecucao_ComOsAprovada_TransitaECopiaMaoDeObraOrcada()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        Assert.Equal(StatusOrdemServico.EmExecucao, os.Status);
        Assert.Equal(OrdemDeServicoTestBuilder.MaoDeObraOrcada, os.MaoDeObraFinal);
    }

    [Fact]
    public void ConcluirExecucao_TransitaParaPronta()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        Assert.Equal(StatusOrdemServico.Pronta, os.Status);
    }

    [Fact]
    public void Entregar_ComOsPronta_TransitaParaEntregue()
    {
        var os = OrdemDeServicoTestBuilder.AteEntregue();

        Assert.Equal(StatusOrdemServico.Entregue, os.Status);
        Assert.NotNull(os.DataEntrega);
        Assert.NotNull(os.GarantiaAte);
    }

    [Theory]
    [InlineData(StatusOrdemServico.Entregue)]
    [InlineData(StatusOrdemServico.DevolvidaSemReparo)]
    [InlineData(StatusOrdemServico.Cancelada)]
    public void EstadosTerminais_NaoTemNenhumaTransicaoPermitida(StatusOrdemServico terminal)
    {
        var os = terminal switch
        {
            StatusOrdemServico.Entregue => OrdemDeServicoTestBuilder.AteEntregue(),
            StatusOrdemServico.DevolvidaSemReparo => DevolvidaComTaxaZero(),
            StatusOrdemServico.Cancelada => Cancelada(),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal))
        };

        // Não há mais nada a tentar num terminal: cancelar de novo é sempre a prova mais simples.
        var resultado = os.Cancelar("tentativa pós-terminal", OrdemDeServicoTestBuilder.Abertura.AddDays(10));

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData(0)] // Aberta
    [InlineData(1)] // EmDiagnostico
    [InlineData(2)] // AguardandoAprovacao
    [InlineData(3)] // Aprovada
    [InlineData(4)] // EmExecucao
    public void Cancelar_EmQualquerEstadoPreEntrega_TransitaParaCancelada(int etapa)
    {
        OrdemDeServico os = etapa switch
        {
            0 => OrdemDeServicoTestBuilder.AbrirNova(),
            1 => OrdemDeServicoTestBuilder.AteEmDiagnostico(),
            2 => OrdemDeServicoTestBuilder.AteAguardandoAprovacao(),
            3 => OrdemDeServicoTestBuilder.AteAprovada(),
            4 => OrdemDeServicoTestBuilder.AteEmExecucao(),
            _ => throw new ArgumentOutOfRangeException(nameof(etapa))
        };

        var resultado = os.Cancelar("cliente desistiu", OrdemDeServicoTestBuilder.Abertura.AddDays(20));

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusOrdemServico.Cancelada, os.Status);
        Assert.Equal("cliente desistiu", os.MotivoCancelamento);
    }

    [Fact]
    public void Cancelar_MotivoVazio_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();

        var resultado = os.Cancelar("  ", OrdemDeServicoTestBuilder.Abertura.AddDays(1));

        Assert.True(resultado.Falha);
        Assert.Equal("os.motivo_obrigatorio", resultado.Erro.Codigo);
    }

    [Fact]
    public void Cancelar_OsPronta_Falha_PortaoJaFechouParaCancelamento()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        var resultado = os.Cancelar("tarde demais", OrdemDeServicoTestBuilder.Abertura.AddDays(10));

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    private static OrdemDeServico DevolvidaComTaxaZero()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();
        os.RegistrarReprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));
        os.DevolverSemReparo(Money.Zero, OrdemDeServicoTestBuilder.Abertura.AddDays(3));
        return os;
    }

    private static OrdemDeServico Cancelada()
    {
        var os = OrdemDeServicoTestBuilder.AbrirNova();
        os.Cancelar("teste", OrdemDeServicoTestBuilder.Abertura.AddDays(1));
        return os;
    }
}
