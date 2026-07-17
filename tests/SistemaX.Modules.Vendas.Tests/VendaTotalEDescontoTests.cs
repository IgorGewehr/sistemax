using SistemaX.Modules.Vendas.Domain;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Vendas.Tests;

/// <summary>
/// Invariantes de TOTAL e DESCONTO (R1/R8 do projeto: dinheiro é sempre Money, toda operação
/// monetária tem teste). <see cref="Venda.Total"/> nunca é um campo cacheado — cada teste aqui
/// também é, implicitamente, uma checagem de que ele é sempre recalculado a partir das linhas.
/// </summary>
public class VendaTotalEDescontoTests
{
    [Fact]
    public void AdicionarItem_UmItem_TotalIgualAoSubtotalDoItem()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(24.90m), quantidade: 2);

        Assert.Equal(Money.DeReais(49.80m), venda.Total);
        Assert.Equal(Money.DeReais(49.80m), venda.SubtotalItens);
    }

    [Fact]
    public void AdicionarItem_VariosItens_TotalSomaTodosOsSubtotais()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);
        venda.AdicionarItem("leite", "Leite Integral 1L", 2, Money.DeReais(5.49m));
        venda.AdicionarItem("arroz", "Arroz Branco 5kg", 1, Money.DeReais(24.90m));

        Assert.Equal(Money.DeReais(5.49m * 2 + 24.90m), venda.Total);
    }

    [Fact]
    public void AdicionarItem_QuantidadeInvalida_Falha()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);

        var resultado = venda.AdicionarItem("produto-1", "Item", 0, Money.DeReais(10));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.quantidade_invalida", resultado.Erro.Codigo);
        Assert.Empty(venda.Itens);
    }

    [Fact]
    public void AdicionarItem_PrecoNaoPositivo_Falha()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);

        var resultado = venda.AdicionarItem("produto-1", "Item", 1, Money.Zero);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.item.preco_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void RemoverItem_ReduzTotal()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);
        venda.AdicionarItem("leite", "Leite", 1, Money.DeReais(5));
        venda.AdicionarItem("arroz", "Arroz", 1, Money.DeReais(20));
        var itemARemover = venda.Itens.Single(i => i.ProdutoId == "arroz");

        var resultado = venda.RemoverItem(itemARemover.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(5), venda.Total);
        Assert.Single(venda.Itens);
    }

    [Fact]
    public void RemoverItem_Inexistente_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(10));

        var resultado = venda.RemoverItem("item-que-nao-existe");

        Assert.True(resultado.Falha);
        Assert.Equal("venda.item.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public void AlterarQuantidadeItem_RecalculaSubtotalDoItemEDaVenda()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(10), quantidade: 1);
        var item = venda.Itens.Single();

        var resultado = venda.AlterarQuantidadeItem(item.Id, 3);

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(30), venda.Total);
    }

    [Fact]
    public void AlterarQuantidadeItem_ZeroOuNegativo_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(10));
        var item = venda.Itens.Single();

        var resultado = venda.AlterarQuantidadeItem(item.Id, 0);

        Assert.True(resultado.Falha);
        Assert.Equal("venda.quantidade_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void AplicarDescontoItem_ReduzSubtotalDoItemETotalDaVenda()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50), quantidade: 1);
        var item = venda.Itens.Single();

        var resultado = venda.AplicarDescontoItem(item.Id, Money.DeReais(10));

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(10), venda.Itens.Single().Desconto);
        Assert.Equal(Money.DeReais(40), venda.Itens.Single().Subtotal);
        Assert.Equal(Money.DeReais(40), venda.Total);
    }

    [Fact]
    public void AplicarDescontoItem_MaiorQueSubtotalBruto_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        var item = venda.Itens.Single();

        var resultado = venda.AplicarDescontoItem(item.Id, Money.DeReais(50.01m));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.item.desconto_maior_que_subtotal", resultado.Erro.Codigo);
        Assert.Equal(Money.Zero, venda.Itens.Single().Desconto); // rejeitado — item não mudou
    }

    [Fact]
    public void AplicarDescontoItem_Negativo_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(50));
        var item = venda.Itens.Single();

        var resultado = venda.AplicarDescontoItem(item.Id, Money.DeReais(-1));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.item.desconto_negativo", resultado.Erro.Codigo);
    }

    [Fact]
    public void AplicarDescontoVenda_ReduzTotalSemAlterarItens()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(100));

        var resultado = venda.AplicarDescontoVenda(Money.DeReais(15), "cliente fidelidade");

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.DeReais(100), venda.SubtotalItens); // subtotal dos itens não muda
        Assert.Equal(Money.DeReais(85), venda.Total);
        Assert.Equal("cliente fidelidade", venda.MotivoDescontoVenda);
    }

    [Fact]
    public void AplicarDescontoVenda_MaiorQueSubtotalDosItens_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(100));

        var resultado = venda.AplicarDescontoVenda(Money.DeReais(100.01m));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.desconto_maior_que_subtotal", resultado.Erro.Codigo);
        Assert.Equal(Money.Zero, venda.DescontoVenda);
    }

    [Fact]
    public void AplicarDescontoVenda_Negativo_Falha()
    {
        var venda = VendaTestBuilder.AbrirComItem(Money.DeReais(100));

        var resultado = venda.AplicarDescontoVenda(Money.DeReais(-1));

        Assert.True(resultado.Falha);
        Assert.Equal("venda.desconto_negativo", resultado.Erro.Codigo);
    }

    [Fact]
    public void DescontoDeItemEDescontoDeVenda_Combinados_TotalReflecteOsDois()
    {
        var venda = Venda.Abrir(VendaTestBuilder.TenantId);
        venda.AdicionarItem("p1", "Produto 1", 1, Money.DeReais(100));
        venda.AdicionarItem("p2", "Produto 2", 1, Money.DeReais(50));
        var item1 = venda.Itens.Single(i => i.ProdutoId == "p1");

        venda.AplicarDescontoItem(item1.Id, Money.DeReais(20)); // subtotalItens = 80 + 50 = 130
        venda.AplicarDescontoVenda(Money.DeReais(30));          // total = 130 - 30 = 100

        Assert.Equal(Money.DeReais(130), venda.SubtotalItens);
        Assert.Equal(Money.DeReais(100), venda.Total);
    }
}
