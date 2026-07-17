using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// Contrato de ESTOQUE (§6 do plano) — o módulo Estoque ainda não existe, mas a OS já grava o
/// fato de reserva/baixa/liberação/estorno como evento de DOMÍNIO com chave de idempotência
/// estável por linha (nunca timestamp), pronto para ganhar tradução para evento de INTEGRAÇÃO no
/// dia em que Estoque nascer (ver OrdemDeServicoDomainEvents.cs).
/// </summary>
public class OrdemDeServicoEstoqueEventosTests
{
    [Fact]
    public void RegistrarAprovacao_LevantaPecaReservadaParaCadaPecaComProdutoDeCatalogo()
    {
        var os = OrdemDeServicoTestBuilder.AteAprovada();

        var evento = Assert.Single(os.DomainEvents.OfType<PecaReservadaDomainEvent>());
        Assert.Equal(os.Id, evento.OrdemServicoId);
        Assert.Equal(OrdemDeServicoTestBuilder.ProdutoIdPecaOrcada, evento.ProdutoId);
        Assert.Equal($"os.reserva:{os.Id}:{evento.LinhaId}", evento.ChaveIdempotencia);
    }

    [Fact]
    public void RegistrarAprovacao_PecaSobEncomendaSemProdutoId_NaoLevantaReserva()
    {
        var os = OrdemDeServicoTestBuilder.AteEmDiagnostico();
        var pecaLivre = PecaOrcada.Nova(null, "Peça a definir com o fornecedor", 1, Money.DeReais(100));
        os.EnviarOrcamento([pecaLivre], Money.DeReais(50), 10, OrdemDeServicoTestBuilder.Abertura.AddDays(2));

        os.RegistrarAprovacao(CanalAprovacao.WhatsApp, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));

        Assert.Empty(os.DomainEvents.OfType<PecaReservadaDomainEvent>());
    }

    [Fact]
    public void AplicarPeca_LevantaPecaConsumidaComMesmaLinhaIdDaReserva()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var linhaId = os.Orcamento!.Pecas.Single().LinhaId;
        var reserva = os.DomainEvents.OfType<PecaReservadaDomainEvent>().Single();

        os.AplicarPeca(linhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1));

        var consumo = Assert.Single(os.DomainEvents.OfType<PecaConsumidaDomainEvent>());
        Assert.Equal(reserva.LinhaId, consumo.LinhaId); // mesma linha reserva → baixa: idempotência por construção
        Assert.Equal($"os.baixa:{os.Id}:{linhaId}", consumo.ChaveIdempotencia);
    }

    [Fact]
    public void AdicionarPecaExtra_ComProdutoDeCatalogo_LevantaPecaConsumida()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        os.AdicionarPecaExtra(
            "produto-parafuso", "Kit parafusos", 1, Money.DeReais(15), clienteAvisado: true,
            agora: OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(2));

        var consumo = Assert.Single(os.DomainEvents.OfType<PecaConsumidaDomainEvent>());
        Assert.Equal("produto-parafuso", consumo.ProdutoId);
    }

    [Fact]
    public void AdicionarPecaExtra_SemProdutoDeCatalogo_NaoLevantaEventoDeEstoque()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();

        os.AdicionarPecaExtra(
            null, "Peça sob encomenda", 1, Money.DeReais(15), clienteAvisado: true,
            agora: OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(2));

        Assert.Empty(os.DomainEvents.OfType<PecaConsumidaDomainEvent>());
    }

    [Fact]
    public void ConcluirExecucao_PecaOrcadaNaoAplicada_LiberaAReserva()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao(); // não aplica a peça orçada

        os.ConcluirExecucao(OrdemDeServicoTestBuilder.Abertura.AddDays(4));

        var liberada = Assert.Single(os.DomainEvents.OfType<ReservaLiberadaDomainEvent>());
        Assert.Equal(OrdemDeServicoTestBuilder.ProdutoIdPecaOrcada, liberada.ProdutoId);
    }

    [Fact]
    public void ConcluirExecucao_PecaOrcadaAplicada_NaoLiberaReserva()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta(); // aplica a única peça orçada antes de concluir

        Assert.Empty(os.DomainEvents.OfType<ReservaLiberadaDomainEvent>());
    }

    [Fact]
    public void Cancelar_AntesDeAprovada_NaoLevantaNenhumEventoDeEstoque()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao(); // ainda não houve reserva

        os.Cancelar("cliente sumiu", OrdemDeServicoTestBuilder.Abertura.AddDays(10));

        Assert.Empty(os.DomainEvents.OfType<ReservaLiberadaDomainEvent>());
        Assert.Empty(os.DomainEvents.OfType<ConsumoEstornadoDomainEvent>());
    }

    [Fact]
    public void Cancelar_EntreAprovadaEExecucao_LiberaTodasAsReservas()
    {
        var os = OrdemDeServicoTestBuilder.AteAprovada();

        os.Cancelar("cliente desistiu antes de iniciar", OrdemDeServicoTestBuilder.Abertura.AddDays(10));

        Assert.Single(os.DomainEvents.OfType<ReservaLiberadaDomainEvent>());
        Assert.Empty(os.DomainEvents.OfType<ConsumoEstornadoDomainEvent>());
    }

    [Fact]
    public void Cancelar_DuranteExecucaoComPecaJaAplicada_LiberaRestanteEEstornaAplicada()
    {
        var os = OrdemDeServicoTestBuilder.AteEmExecucao();
        var linhaId = os.Orcamento!.Pecas.Single().LinhaId;
        os.AplicarPeca(linhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1)); // única peça orçada — aplicada

        os.Cancelar("equipamento com defeito irreparável", OrdemDeServicoTestBuilder.Abertura.AddDays(4));

        // nada sobrou pra liberar (a única linha orçada já foi aplicada); o estorno é do que já foi baixado.
        Assert.Empty(os.DomainEvents.OfType<ReservaLiberadaDomainEvent>());
        var estorno = Assert.Single(os.DomainEvents.OfType<ConsumoEstornadoDomainEvent>());
        Assert.Equal(linhaId, estorno.LinhaId);
    }
}
