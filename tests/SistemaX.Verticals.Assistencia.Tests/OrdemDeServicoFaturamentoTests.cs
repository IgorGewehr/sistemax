using SistemaX.Modules.Abstractions;
using SistemaX.SharedKernel;
using SistemaX.Verticals.Assistencia;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// Faturamento (§7 do plano): <c>Entregar()</c> fatura E entrega no MESMO ato — nunca dois
/// estados separados. Cobre o evento de domínio <see cref="OsFaturadaDomainEvent"/>, a tradução
/// para o evento de integração <see cref="OsFaturada"/> já catalogado em Modules.Abstractions
/// (mesmo mecanismo de <c>Venda.Concluir → VendaConcluida</c>), a regra "desconto abate mão de
/// obra primeiro" e o caso de garantia com total zero que NÃO fatura.
/// </summary>
public class OrdemDeServicoFaturamentoTests
{
    [Fact]
    public void Entregar_SemDesconto_LevantaOsFaturadaDomainEventComValoresDoOrcamento()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        var resultado = os.Entregar(FormaPagamento.Pix, Money.Zero, 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        Assert.True(resultado.Sucesso);
        var evento = Assert.Single(os.DomainEvents.OfType<OsFaturadaDomainEvent>());
        Assert.Equal(os.Id, evento.OrdemServicoId);
        Assert.Equal(OrdemDeServicoTestBuilder.TenantId, evento.TenantId);
        Assert.Equal(OrdemDeServicoTestBuilder.MaoDeObraOrcada, evento.ValorServico);
        Assert.Equal(OrdemDeServicoTestBuilder.PrecoPecaOrcada, evento.ValorPecas);
        Assert.Equal("cliente-1", evento.ClienteId);
        Assert.Equal("Pedro Lima", evento.ClienteNome);
        Assert.Equal("OS-0001", evento.NumeroOs);
        Assert.Equal(FormaPagamento.Pix, evento.FormaPagamento);
        Assert.Equal("tecnico-1", evento.TecnicoId);
    }

    [Fact]
    public void Entregar_ParaEventoDeIntegracao_ProduzOsFaturadaComChaveIdempotenciaDoFato()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();
        os.Entregar(FormaPagamento.Dinheiro, Money.Zero, 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        var domainEvent = os.DomainEvents.OfType<OsFaturadaDomainEvent>().Single();
        var eventoDeIntegracao = domainEvent.ParaEventoDeIntegracao();

        Assert.Equal(os.Id, eventoDeIntegracao.OrdemServicoId);
        Assert.Equal(12_000, eventoDeIntegracao.ValorServicoCentavos);
        Assert.Equal(39_000, eventoDeIntegracao.ValorPecasCentavos);
        Assert.Equal($"os.faturada:{os.Id}", eventoDeIntegracao.ChaveIdempotencia);
    }

    [Fact]
    public void Entregar_DescontoMenorQueMaoDeObra_AbateSoAMaoDeObra()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta(); // mão de obra 120, peças 390

        os.Entregar(FormaPagamento.CartaoDebito, Money.DeReais(50), 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        var evento = os.DomainEvents.OfType<OsFaturadaDomainEvent>().Single();
        Assert.Equal(Money.DeReais(70), evento.ValorServico);   // 120 - 50
        Assert.Equal(Money.DeReais(390), evento.ValorPecas);    // intacto
    }

    [Fact]
    public void Entregar_DescontoMaiorQueMaoDeObra_TransbordaParaPecas()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta(); // mão de obra 120, peças 390

        os.Entregar(FormaPagamento.CartaoCredito, Money.DeReais(150), 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        var evento = os.DomainEvents.OfType<OsFaturadaDomainEvent>().Single();
        Assert.Equal(Money.Zero, evento.ValorServico);       // 120 - 150 => zerado, sobra 30 pra peça
        Assert.Equal(Money.DeReais(360), evento.ValorPecas); // 390 - 30
    }

    [Fact]
    public void Entregar_DescontoMaiorQueTotal_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        var resultado = os.Entregar(FormaPagamento.Pix, Money.DeReais(9999), 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        Assert.True(resultado.Falha);
        Assert.Equal("os.desconto_maior_que_total", resultado.Erro.Codigo);
    }

    [Fact]
    public void Entregar_GarantiaDiasNegativa_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AtePronta();

        var resultado = os.Entregar(FormaPagamento.Pix, Money.Zero, -1, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        Assert.True(resultado.Falha);
        Assert.Equal("os.garantia_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Entregar_CalculaGarantiaAteAPartirDaDataDeEntrega()
    {
        var os = OrdemDeServicoTestBuilder.AteEntregue(garantiaDias: 90);

        Assert.Equal(os.DataEntrega!.Value.AddDays(90), os.GarantiaAte);
    }

    [Fact]
    public void Entregar_OsDeGarantiaComTotalZero_NaoLevantaOsFaturada()
    {
        // OS de retorno em garantia: orçamento nasce zerado (peça em garantia custa 0 ao
        // cliente) — plano §5.4: nada a receber, não fatura.
        var os = OrdemDeServico.Abrir(
            OrdemDeServicoTestBuilder.TenantId, "OS-0002-GAR", OrdemDeServicoTestBuilder.Cliente(),
            OrdemDeServicoTestBuilder.Equipamento(), "mesmo defeito da OS-0001", OrdemDeServicoTestBuilder.Abertura,
            osOrigemId: "os-0001-original");
        os.AtribuirTecnico("tecnico-1", "Igor");
        os.RegistrarDiagnostico("Fonte com o mesmo defeito.", OrdemDeServicoTestBuilder.Abertura.AddDays(1));

        var pecaEmGarantia = PecaOrcada.Nova("produto-fonte-1", "Fonte ADP-400DR (garantia)", 1, Money.Zero);
        os.EnviarOrcamento([pecaEmGarantia], Money.Zero, 10, OrdemDeServicoTestBuilder.Abertura.AddDays(2));
        os.RegistrarAprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));
        os.IniciarExecucao(OrdemDeServicoTestBuilder.Abertura.AddDays(3));
        os.AplicarPeca(pecaEmGarantia.LinhaId, OrdemDeServicoTestBuilder.Abertura.AddDays(3).AddHours(1));
        os.ConcluirExecucao(OrdemDeServicoTestBuilder.Abertura.AddDays(4));

        Assert.True(os.EhRetornoDeGarantia);
        Assert.True(os.TotalGeral.EhZero);

        var resultado = os.Entregar(FormaPagamento.Dinheiro, Money.Zero, 90, OrdemDeServicoTestBuilder.Abertura.AddDays(5));

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusOrdemServico.Entregue, os.Status);
        Assert.Empty(os.DomainEvents.OfType<OsFaturadaDomainEvent>());
        // o consumo de peça em garantia ainda é rastreado (custo real sem receita — a verdade
        // que o plano §5.4 pede): o evento de baixa foi levantado em AplicarPeca.
        Assert.Single(os.DomainEvents.OfType<PecaConsumidaDomainEvent>());
    }

    [Fact]
    public void DevolverSemReparo_ComTaxaDeDiagnostico_LevantaOsFaturadaSoDeServico()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();
        os.RegistrarReprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));

        var resultado = os.DevolverSemReparo(Money.DeReais(50), OrdemDeServicoTestBuilder.Abertura.AddDays(3));

        Assert.True(resultado.Sucesso);
        var evento = Assert.Single(os.DomainEvents.OfType<OsFaturadaDomainEvent>());
        Assert.Equal(Money.DeReais(50), evento.ValorServico);
        Assert.Equal(Money.Zero, evento.ValorPecas);
    }

    [Fact]
    public void DevolverSemReparo_TaxaZero_NaoLevantaOsFaturada()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();
        os.RegistrarReprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));

        os.DevolverSemReparo(Money.Zero, OrdemDeServicoTestBuilder.Abertura.AddDays(3));

        Assert.Empty(os.DomainEvents.OfType<OsFaturadaDomainEvent>());
    }

    [Fact]
    public void DevolverSemReparo_TaxaNegativa_Falha()
    {
        var os = OrdemDeServicoTestBuilder.AteAguardandoAprovacao();
        os.RegistrarReprovacao(CanalAprovacao.Presencial, OrdemDeServicoTestBuilder.Abertura.AddDays(2).AddHours(1));

        var resultado = os.DevolverSemReparo(Money.DeReais(-1), OrdemDeServicoTestBuilder.Abertura.AddDays(3));

        Assert.True(resultado.Falha);
        Assert.Equal("os.taxa_diagnostico_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void ClearDomainEvents_EsvaziaAListaAposPublicacao()
    {
        var os = OrdemDeServicoTestBuilder.AteEntregue();

        os.ClearDomainEvents();

        Assert.Empty(os.DomainEvents);
    }
}
