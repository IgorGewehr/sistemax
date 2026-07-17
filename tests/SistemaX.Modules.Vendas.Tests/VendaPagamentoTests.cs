using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>
/// Invariantes de PAGAMENTO: split natural (várias linhas cobrindo o total), troco correto e só
/// em dinheiro, e a trava "pagamento nunca excede o restante" (nem em split, nem em dinheiro —
/// o excedente em dinheiro vira <see cref="PagamentoDeVenda.Troco"/>, nunca <see cref="PagamentoDeVenda.Valor"/>).
/// </summary>
public class VendaPagamentoTests
{
    [Fact]
    public void RegistrarPagamento_UnicoCobrindoOTotal_ZeraRestante()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(54.31m));

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(54.31m), null, DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.True(venda.Restante.EhZero);
        Assert.Equal(Money.DeReais(54.31m), venda.TotalPago);
    }

    [Fact]
    public void RegistrarPagamento_Split_DoisMetodosSomandoOTotal_RestanteZeraNoSegundo()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(150));

        var primeiro = venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(100), null, DateTimeOffset.UtcNow);
        Assert.True(primeiro.Sucesso);
        Assert.Equal(Money.DeReais(50), venda.Restante);

        var segundo = venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(50), Money.DeReais(50), DateTimeOffset.UtcNow);

        Assert.True(segundo.Sucesso);
        Assert.True(venda.Restante.EhZero);
        Assert.Equal(2, venda.Pagamentos.Count);
    }

    [Fact]
    public void RegistrarPagamento_ExcedeRestante_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Credito, Money.DeReais(50.01m), null, DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento_excede_restante", resultado.Erro.Codigo);
        Assert.Empty(venda.Pagamentos);
    }

    [Fact]
    public void RegistrarPagamento_ValorNaoPositivo_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.Zero, null, DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento.valor_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void RegistrarPagamento_Dinheiro_ComValorRecebidoMaior_CalculaTrocoCorretamente()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(54.31m));

        venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(54.31m), Money.DeReais(100), DateTimeOffset.UtcNow);

        var pagamento = venda.Pagamentos.Single();
        Assert.Equal(Money.DeReais(45.69m), pagamento.Troco);
    }

    [Fact]
    public void RegistrarPagamento_TrocoEmMetodoQueNaoEDinheiro_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), Money.DeReais(60), DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento.troco_apenas_dinheiro", resultado.Erro.Codigo);
    }

    [Fact]
    public void RegistrarPagamento_DinheiroComValorRecebidoMenorQueValor_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(50), Money.DeReais(30), DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento.recebido_insuficiente", resultado.Erro.Codigo);
    }

    [Fact]
    public void RegistrarPagamento_SemNenhumItem_Falha()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);

        var resultado = venda.RegistrarPagamento(MetodoPagamento.Dinheiro, Money.DeReais(10), null, DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.sem_itens", resultado.Erro.Codigo);
    }

    [Fact]
    public void RemoverPagamento_LiberaORestanteParaNovoPagamento()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        venda.RegistrarPagamento(MetodoPagamento.Pix, Money.DeReais(50), null, DateTimeOffset.UtcNow);
        var idPagamento = venda.Pagamentos.Single().Id;

        var remocao = venda.RemoverPagamento(idPagamento);

        Assert.True(remocao.Sucesso);
        Assert.Equal(Money.DeReais(50), venda.Restante);
        Assert.Empty(venda.Pagamentos);
    }

    [Fact]
    public void RemoverPagamento_Inexistente_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));

        var resultado = venda.RemoverPagamento("pagamento-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("venda.pagamento.nao_encontrado", resultado.Erro.Codigo);
    }
}
