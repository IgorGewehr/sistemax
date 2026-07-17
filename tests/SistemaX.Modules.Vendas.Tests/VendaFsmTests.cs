using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>
/// FSM de <see cref="Venda"/> (R4/R8 do projeto): <c>Aberta → Concluida → Estornada</c>, sem
/// atalho e sem volta. Cobre transição válida E inválida (nunca só o caminho feliz), e a
/// invariante extra de MONTAGEM vs PAGAMENTO documentada em <see cref="Venda"/>.
/// </summary>
public class VendaFsmTests
{
    [Fact]
    public void Abrir_ComecaNoStatusAberta()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);

        Assert.Equal(StatusVenda.Aberta, venda.Status);
    }

    [Fact]
    public void Abrir_TenantIdVazio_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Venda.Abrir(" "));
    }

    [Fact]
    public void Concluir_SemItens_Falha()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);

        var resultado = venda.Concluir();

        Assert.True(resultado.Falha);
        Assert.Equal("venda.sem_itens", resultado.Erro.Codigo);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
    }

    [Fact]
    public void Concluir_SemNenhumPagamento_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.Concluir();

        Assert.True(resultado.Falha);
        Assert.Equal("venda.sem_pagamento", resultado.Erro.Codigo);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
    }

    [Fact]
    public void Concluir_ComPagamentoParcial_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(20), valorRecebido: null, DateTimeOffset.UtcNow);

        var resultado = venda.Concluir();

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento_incompleto", resultado.Erro.Codigo);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
    }

    [Fact]
    public void Concluir_ComPagamentoCompleto_TransitaParaConcluida()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), valorRecebido: null, DateTimeOffset.UtcNow);

        var resultado = venda.Concluir();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusVenda.Concluida, venda.Status);
    }

    [Fact]
    public void Concluir_VendaJaConcluida_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();

        var resultado = venda.Concluir();

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Estornar_VendaAindaAberta_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.Estornar();

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
        Assert.Equal(StatusVenda.Aberta, venda.Status);
    }

    [Fact]
    public void Estornar_VendaConcluida_TransitaParaEstornada()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Debito, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();

        var resultado = venda.Estornar();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusVenda.Estornada, venda.Status);
    }

    [Fact]
    public void Estornar_VendaJaEstornada_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Debito, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();
        venda.Estornar();

        var resultado = venda.Estornar();

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData(StatusVenda.Concluida)]
    [InlineData(StatusVenda.Estornada)]
    public void RegistrarPagamento_VendaForaDeAberta_Falha(StatusVenda statusAlvo)
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        venda.Concluir();
        if (statusAlvo == StatusVenda.Estornada) venda.Estornar();

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(1), null, DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.status_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void AdicionarItem_DepoisDoPrimeiroPagamento_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(20), null, DateTimeOffset.UtcNow);

        var resultado = venda.AdicionarItem("produto-2", "Outro item", 1, Money.DeReais(10));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento_ja_iniciado", resultado.Erro.Codigo);
    }

    [Fact]
    public void AplicarDescontoVenda_DepoisDoPrimeiroPagamento_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(20), null, DateTimeOffset.UtcNow);

        var resultado = venda.AplicarDescontoVenda(Money.DeReais(5));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento_ja_iniciado", resultado.Erro.Codigo);
    }

    [Fact]
    public void RemoverPagamento_LiberaMontagemDeNovo()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(20), null, DateTimeOffset.UtcNow);
        var idPagamento = venda.Pagamentos.Single().Id;

        venda.RemoverPagamento(idPagamento);
        var resultado = venda.AplicarDescontoVenda(Money.DeReais(5));

        Assert.True(resultado.Sucesso);
    }
}
