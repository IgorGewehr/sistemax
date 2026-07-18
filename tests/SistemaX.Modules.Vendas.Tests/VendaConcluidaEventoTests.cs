using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Vendas.Application.CasosDeUso;
using SistemaX.Modules.Vendas.Domain;
using SistemaX.Modules.Vendas.Infrastructure.InMemory;
using SistemaX.Modules.Vendas.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>
/// Evento de DOMÍNIO (<see cref="VendaConcluidaDomainEvent"/>/<see cref="VendaEstornadaDomainEvent"/>)
/// vs evento de INTEGRAÇÃO (<see cref="VendaConcluida"/>/<see cref="VendaEstornada"/>,
/// Modules.Abstractions) — ver o cabeçalho de VendaDomainEvents.cs para a distinção completa.
/// Os testes de caso de uso aqui também verificam a ORDEM commit-depois-publica (R3 do projeto).
/// </summary>
public class VendaConcluidaEventoTests
{
    [Fact]
    public void Concluir_AcumulaVendaConcluidaDomainEvent_ComTotalEFormaPagamentoPrincipal()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);

        venda.Concluir();

        var evento = Assert.Single(venda.DomainEvents.OfType<VendaConcluidaDomainEvent>());
        Assert.Equal(venda.Id, evento.VendaId);
        Assert.Equal(VendaTestBuilder.TenantId, evento.TenantId);
        Assert.Equal(Money.DeReais(50), evento.Total);
        Assert.Equal(nameof(MetodoPagamento.Pix), evento.FormaPagamento);
    }

    [Fact]
    public void Concluir_ParaEventoDeIntegracao_ProduzVendaConcluidaComChaveIdempotenciaDoFato()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(120));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(120), Money.DeReais(150), DateTimeOffset.UtcNow);
        venda.Concluir();

        var domainEvent = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        var eventoDeIntegracao = domainEvent.ParaEventoDeIntegracao();

        Assert.Equal(venda.Id, eventoDeIntegracao.VendaId);
        Assert.Equal(12_000, eventoDeIntegracao.TotalCentavos);
        Assert.Equal(nameof(MetodoPagamento.Dinheiro), eventoDeIntegracao.FormaPagamento);
        Assert.Equal($"venda.concluida:{venda.Id}", eventoDeIntegracao.ChaveIdempotencia);
    }

    [Fact]
    public void Concluir_ComSplitDePagamento_FormaPagamentoPrincipalEhOMetodoDeMaiorValor()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(150));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(30), null, DateTimeOffset.UtcNow);
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(120), null, DateTimeOffset.UtcNow);

        venda.Concluir();

        var evento = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        Assert.Equal(nameof(MetodoPagamento.Pix), evento.FormaPagamento); // 120 > 30
    }

    /// <summary>P2-2 (docs/financeiro/revisao-domain-fit-cnpj.md) — o evento de INTEGRAÇÃO carrega
    /// TODOS os pagamentos do split, não só o principal, pra <c>FatoRecebiveisProjection</c> foldar
    /// MDR/lag POR forma.</summary>
    [Fact]
    public void Concluir_ComSplitDePagamento_ParaEventoDeIntegracao_CarregaTodosOsPagamentos()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(150));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(30), null, DateTimeOffset.UtcNow);
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(120), null, DateTimeOffset.UtcNow);
        venda.Concluir();

        var domainEvent = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        var eventoDeIntegracao = domainEvent.ParaEventoDeIntegracao();

        Assert.NotNull(eventoDeIntegracao.Pagamentos);
        Assert.Equal(2, eventoDeIntegracao.Pagamentos!.Count);
        Assert.Contains(eventoDeIntegracao.Pagamentos, p => p.Metodo == nameof(MetodoPagamento.Dinheiro) && p.ValorCentavos == 3_000);
        Assert.Contains(eventoDeIntegracao.Pagamentos, p => p.Metodo == nameof(MetodoPagamento.Pix) && p.ValorCentavos == 12_000);
    }

    /// <summary>Com 1 pagamento só, <c>Pagamentos</c> continua vindo preenchido com 1 linha (não
    /// <c>null</c>) — quem consome já trata "1 linha" como "sem split real" (P2-2), então não há
    /// ambiguidade em manter o campo sempre populado quando há pagamento.</summary>
    [Fact]
    public void Concluir_ComUmSoPagamento_ParaEventoDeIntegracao_CarregaUmaLinhaEmPagamentos()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();

        var domainEvent = venda.DomainEvents.OfType<VendaConcluidaDomainEvent>().Single();
        var eventoDeIntegracao = domainEvent.ParaEventoDeIntegracao();

        var pagamento = Assert.Single(eventoDeIntegracao.Pagamentos!);
        Assert.Equal(nameof(MetodoPagamento.Pix), pagamento.Metodo);
        Assert.Equal(5_000, pagamento.ValorCentavos);
    }

    [Fact]
    public void ClearDomainEvents_EsvaziaAListaAposPublicacao()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();

        venda.ClearDomainEvents();

        Assert.Empty(venda.DomainEvents);
    }

    [Fact]
    public void Estornar_ParaEventoDeIntegracao_ProduzVendaEstornadaComChaveIdempotenciaDoFato()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(80));
        venda.RegistrarPagamento(MetodoPagamento.Debito, Money.DeReais(80), null, DateTimeOffset.UtcNow);
        venda.Concluir();
        venda.ClearDomainEvents();

        venda.Estornar();

        var domainEvent = venda.DomainEvents.OfType<VendaEstornadaDomainEvent>().Single();
        var eventoDeIntegracao = domainEvent.ParaEventoDeIntegracao();

        Assert.Equal(8_000, eventoDeIntegracao.TotalCentavos);
        Assert.Equal($"venda.estornada:{venda.Id}", eventoDeIntegracao.ChaveIdempotencia);
    }

    [Fact]
    public async Task ConcluirVendaUseCase_SalvaAntesDePublicar_EPublicaVendaConcluidaEVendaItensMovimentados()
    {
        var repositorio = new InMemoryVendaRepository();
        var bus = new FakeIntegrationEventBus();
        var iniciar = new IniciarVendaUseCase(repositorio);
        var montar = new MontarVendaUseCase(repositorio);
        var concluir = new ConcluirVendaUseCase(repositorio, bus);

        var venda = (await iniciar.ExecutarAsync(VendaTestBuilder.TenantId)).Valor;
        await montar.AdicionarItemAsync(venda.Id, "produto-1", "Item", 1, Money.DeReais(54.31m));
        await montar.RegistrarPagamentoAsync(venda.Id, MetodoPagamento.Pix, Money.DeReais(54.31m), null, DateTimeOffset.UtcNow);

        var resultado = await concluir.ExecutarAsync(venda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusVenda.Concluida, resultado.Valor.Status);

        var persistida = await repositorio.ObterPorIdAsync(venda.Id);
        Assert.Equal(StatusVenda.Concluida, persistida!.Status); // commit local aconteceu

        Assert.Equal(2, bus.Publicados.Count); // VendaConcluida (Financeiro) + VendaItensMovimentados (Estoque/Fiscal)
        var publicados = bus.Publicados.ToArray();
        var vendaConcluida = Assert.IsType<VendaConcluida>(publicados[0]);
        Assert.Equal(venda.Id, vendaConcluida.VendaId);
        Assert.Equal(5_431, vendaConcluida.TotalCentavos);

        var vendaItensMovimentados = Assert.IsType<VendaItensMovimentados>(publicados[1]);
        Assert.Equal(venda.Id, vendaItensMovimentados.VendaId);
        Assert.Equal(VendaTestBuilder.TenantId, vendaItensMovimentados.TenantId);
        var item = Assert.Single(vendaItensMovimentados.Itens);
        Assert.Equal("produto-1", item.ProdutoId);
        Assert.Equal(1_000, item.QuantidadeMilesimos); // 1 unidade inteira = 1000 milésimos
        Assert.Equal(5_431, item.PrecoUnitarioCentavos);
        Assert.Equal($"venda.itens:{venda.Id}", vendaItensMovimentados.ChaveIdempotencia);

        Assert.Empty(resultado.Valor.DomainEvents); // ClearDomainEvents() rodou depois de publicar
    }

    [Fact]
    public async Task EstornarVendaUseCase_SalvaAntesDePublicar_EPublicaVendaEstornada()
    {
        var repositorio = new InMemoryVendaRepository();
        var bus = new FakeIntegrationEventBus();
        var iniciar = new IniciarVendaUseCase(repositorio);
        var montar = new MontarVendaUseCase(repositorio);
        var concluir = new ConcluirVendaUseCase(repositorio, bus);
        var estornar = new EstornarVendaUseCase(repositorio, bus);

        var venda = (await iniciar.ExecutarAsync(VendaTestBuilder.TenantId)).Valor;
        await montar.AdicionarItemAsync(venda.Id, "produto-1", "Item", 1, Money.DeReais(10));
        await montar.RegistrarPagamentoAsync(venda.Id, MetodoPagamento.Dinheiro, Money.DeReais(10), null, DateTimeOffset.UtcNow);
        await concluir.ExecutarAsync(venda.Id);

        var resultado = await estornar.ExecutarAsync(venda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusVenda.Estornada, resultado.Valor.Status);

        Assert.Equal(3, bus.Publicados.Count); // VendaConcluida + VendaItensMovimentados + VendaEstornada
        Assert.IsType<VendaEstornada>(bus.Publicados.Last());
    }

    [Fact]
    public async Task ConcluirVendaUseCase_VendaInexistente_Falha()
    {
        var repositorio = new InMemoryVendaRepository();
        var bus = new FakeIntegrationEventBus();
        var concluir = new ConcluirVendaUseCase(repositorio, bus);

        var resultado = await concluir.ExecutarAsync("venda-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("venda.nao_encontrada", resultado.Erro.Codigo);
        Assert.Empty(bus.Publicados);
    }
}
