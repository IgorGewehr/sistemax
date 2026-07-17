using SistemaX.Modules.Estoque.Application.Comum;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;

namespace SistemaX.Modules.Estoque.Tests;

public class ExpansorDeFichaTecnicaTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task ExpandirAsync_ProdutoSemFicha_RetornaOProprioProdutoComAQuantidadePedida()
    {
        var produtos = new InMemoryProdutoRepository();
        var produto = Produto.Criar(TenantId, "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produto);

        var resultado = await ExpansorDeFichaTecnica.ExpandirAsync(produtos, produto.Id, Quantidade.DeInteiro(3));

        Assert.True(resultado.Sucesso);
        var (produtoId, quantidade) = Assert.Single(resultado.Valor);
        Assert.Equal(produto.Id, produtoId);
        Assert.Equal(Quantidade.DeInteiro(3), quantidade);
    }

    [Fact]
    public async Task ExpandirAsync_ComposicaoEmDoisNiveis_ExpandeAteAsFolhasComQuantidadeEscalonada()
    {
        var produtos = new InMemoryProdutoRepository();

        var farinha = Produto.Criar(TenantId, "Farinha", UnidadeDeMedida.KG).Valor;
        await produtos.SalvarAsync(farinha);

        var massa = Produto.Criar(TenantId, "Massa base", UnidadeDeMedida.UN,
            fichaTecnica: [new ComponenteDeFicha(farinha.Id, Quantidade.DeDecimal(0.4m))]).Valor;
        await produtos.SalvarAsync(massa);

        var pizza = Produto.Criar(TenantId, "Pizza", UnidadeDeMedida.UN,
            fichaTecnica: [new ComponenteDeFicha(massa.Id, Quantidade.DeInteiro(1))]).Valor;
        await produtos.SalvarAsync(pizza);

        var resultado = await ExpansorDeFichaTecnica.ExpandirAsync(produtos, pizza.Id, Quantidade.DeInteiro(3));

        Assert.True(resultado.Sucesso);
        var (produtoId, quantidade) = Assert.Single(resultado.Valor);
        Assert.Equal(farinha.Id, produtoId);
        Assert.Equal(Quantidade.DeDecimal(1.2m), quantidade); // 3 pizzas × 1 massa × 0,4 KG
    }

    [Fact]
    public async Task ExpandirAsync_ComCicloNaComposicao_Falha()
    {
        var produtos = new InMemoryProdutoRepository();

        // C referencia D; fecha-se o ciclo reeditando C para referenciar D de volta (ver
        // ReconstruirComFicha) — Produto não expõe mutação pública de ficha hoje, então o teste
        // simula a "edição" reconstruindo o agregado com o mesmo Id.
        var produtoC = Produto.Criar(TenantId, "C", UnidadeDeMedida.UN).Valor;
        await produtos.SalvarAsync(produtoC);

        var produtoD = Produto.Criar(TenantId, "D", UnidadeDeMedida.UN,
            fichaTecnica: [new ComponenteDeFicha(produtoC.Id, Quantidade.DeInteiro(1))]).Valor;
        await produtos.SalvarAsync(produtoD);

        var produtoCComCiclo = ReconstruirComFicha(produtoC, [new ComponenteDeFicha(produtoD.Id, Quantidade.DeInteiro(1))]);
        await produtos.SalvarAsync(produtoCComCiclo);

        var resultado = await ExpansorDeFichaTecnica.ExpandirAsync(produtos, produtoC.Id, Quantidade.DeInteiro(1));

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.bom.ciclo_detectado", resultado.Erro.Codigo);
    }

    /// <summary>Helper de teste: reconstrói um produto com o MESMO Id, mas com ficha técnica
    /// diferente — simula uma edição de ficha via <c>Produto.Criar</c> + persistência por cima
    /// (o agregado real trocaria a ficha por um método de domínio; para o teste de ciclo, o atalho
    /// evita expor esse método só para este cenário adversarial).</summary>
    private static Produto ReconstruirComFicha(Produto original, IReadOnlyList<ComponenteDeFicha> novaFicha)
    {
        var reconstruido = Produto.Criar(original.TenantId, original.Nome, original.Unidade, sku: original.Sku, fichaTecnica: novaFicha).Valor;
        typeof(Produto).GetProperty(nameof(Produto.Id))!.SetValue(reconstruido, original.Id);
        return reconstruido;
    }
}
