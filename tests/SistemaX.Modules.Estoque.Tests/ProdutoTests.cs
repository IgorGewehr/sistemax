using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

public class ProdutoTests
{
    [Fact]
    public void Criar_ComDadosMinimos_GeraSkuEIdAutomaticos()
    {
        var resultado = Produto.Criar("tenant-1", "Cabo USB-C 1m", UnidadeDeMedida.UN);

        Assert.True(resultado.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Valor.Id));
        Assert.StartsWith("SKU-", resultado.Valor.Sku);
        Assert.True(resultado.Valor.Ativo);
        Assert.True(resultado.Valor.ControlaEstoque);
    }

    [Fact]
    public void Criar_SemNome_Falha()
    {
        var resultado = Produto.Criar("tenant-1", "", UnidadeDeMedida.UN);
        Assert.True(resultado.Falha);
        Assert.Equal("estoque.produto.nome_obrigatorio", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComFichaTecnicaDuplicada_Falha()
    {
        var ficha = new[]
        {
            new ComponenteDeFicha("insumo-1", Quantidade.DeDecimal(0.5m)),
            new ComponenteDeFicha("insumo-1", Quantidade.DeDecimal(0.2m))
        };

        var resultado = Produto.Criar("tenant-1", "Pizza Calabresa", UnidadeDeMedida.UN, fichaTecnica: ficha);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.produto.ficha_duplicada", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComQuantidadeDeInsumoNaoPositiva_Falha()
    {
        var ficha = new[] { new ComponenteDeFicha("insumo-1", Quantidade.Zero) };

        var resultado = Produto.Criar("tenant-1", "Pizza Calabresa", UnidadeDeMedida.UN, fichaTecnica: ficha);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.produto.ficha_quantidade_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComEstoqueMinimoNegativo_Falha()
    {
        var resultado = Produto.Criar("tenant-1", "Tela iPhone 13", UnidadeDeMedida.UN, estoqueMinimo: new Quantidade(-1));
        Assert.True(resultado.Falha);
        Assert.Equal("estoque.produto.minimo_negativo", resultado.Erro.Codigo);
    }

    [Fact]
    public void ControlaEstoqueFalse_MarcaProdutoComoServicoOuTaxa()
    {
        var resultado = Produto.Criar("tenant-1", "Taxa de diagnóstico", UnidadeDeMedida.UN, controlaEstoque: false);

        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor.ControlaEstoque);
    }

    [Fact]
    public void Inativar_MarcaAtivoComoFalse()
    {
        var produto = Produto.Criar("tenant-1", "Produto descontinuado", UnidadeDeMedida.UN).Valor;
        produto.Inativar();
        Assert.False(produto.Ativo);
    }

    [Fact]
    public void AdicionarCodigoDeBarrasDuplicado_Falha()
    {
        var produto = Produto.Criar("tenant-1", "Cabo USB-C 1m", UnidadeDeMedida.UN).Valor;
        var codigo = new CodigoDeBarras("7891234567890", TipoCodigoBarras.Ean13);

        Assert.True(produto.AdicionarCodigoDeBarras(codigo).Sucesso);
        Assert.True(produto.AdicionarCodigoDeBarras(codigo).Falha);
    }
}
