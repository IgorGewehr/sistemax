using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Domain.Catalogo;
using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IProdutoRepository"/> — roda EXATAMENTE o mesmo conjunto de
/// casos contra QUALQUER adapter (hoje: <c>InMemoryProdutoRepository</c> e
/// <c>SqliteProdutoRepository</c>). Mesmo molde de <c>FornecedorRepositoryContractTests</c>
/// (Compras) — os NOMES dos métodos de teste documentam o CONTRATO do port, não a implementação.
/// </summary>
public abstract class ProdutoRepositoryContractTests
{
    protected const string TenantA = "loja-a";
    protected const string TenantB = "loja-b";

    protected abstract IProdutoRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_id_com_filhos_retorna_o_mesmo_produto()
    {
        var repo = CriarRepositorio();
        var ficha = new[]
        {
            new ComponenteDeFicha("insumo-1", Quantidade.DeDecimal(0.5m)),
            new ComponenteDeFicha("insumo-2", Quantidade.DeInteiro(2))
        };
        var codigos = new[] { new CodigoDeBarras("7891234567890", TipoCodigoBarras.Ean13) };
        var produto = Produto.Criar(
            TenantA, "Pizza Calabresa", UnidadeDeMedida.UN,
            sku: "PIZZA-CAL",
            precoVenda: Money.DeReais(39.90m),
            categoria: "Pizzas",
            descricao: "Molho, mussarela e calabresa",
            fiscal: new DadosFiscaisProduto("21069090", "017"),
            estoqueMinimo: Quantidade.DeInteiro(5),
            pontoDeReposicao: Quantidade.DeInteiro(10),
            loteEconomico: Quantidade.DeInteiro(20),
            leadTimeDias: 3,
            localizacao: "Prateleira A1",
            controlaEstoque: true,
            controlePorLote: true,
            valorizacao: PoliticaDeValorizacao.CustoMedio,
            fichaTecnica: ficha,
            codigosDeBarras: codigos).Valor;

        await repo.SalvarAsync(produto);
        var lido = await repo.ObterPorIdAsync(produto.Id);

        Assert.NotNull(lido);
        Assert.Equal(produto.Id, lido!.Id);
        Assert.Equal(produto.TenantId, lido.TenantId);
        Assert.Equal(produto.Sku, lido.Sku);
        Assert.Equal(produto.Nome, lido.Nome);
        Assert.Equal(produto.Descricao, lido.Descricao);
        Assert.Equal(produto.Categoria, lido.Categoria);
        Assert.Equal(produto.Unidade, lido.Unidade);
        Assert.Equal(produto.PrecoVenda, lido.PrecoVenda);
        Assert.Equal(produto.Fiscal, lido.Fiscal);
        Assert.Equal(produto.EstoqueMinimo, lido.EstoqueMinimo);
        Assert.Equal(produto.PontoDeReposicao, lido.PontoDeReposicao);
        Assert.Equal(produto.LoteEconomico, lido.LoteEconomico);
        Assert.Equal(produto.LeadTimeDias, lido.LeadTimeDias);
        Assert.Equal(produto.Localizacao, lido.Localizacao);
        Assert.Equal(produto.ControlaEstoque, lido.ControlaEstoque);
        Assert.Equal(produto.ControlePorLote, lido.ControlePorLote);
        Assert.Equal(produto.Valorizacao, lido.Valorizacao);
        Assert.Equal(produto.Ativo, lido.Ativo);

        Assert.Equal(produto.CodigosDeBarras.Count, lido.CodigosDeBarras.Count);
        foreach (var codigo in produto.CodigosDeBarras)
        {
            Assert.Contains(lido.CodigosDeBarras, c => c.Valor == codigo.Valor && c.Tipo == codigo.Tipo);
        }

        Assert.Equal(produto.FichaTecnica.Count, lido.FichaTecnica.Count);
        foreach (var componente in produto.FichaTecnica)
        {
            Assert.Contains(lido.FichaTecnica, c => c.ProdutoInsumoId == componente.ProdutoInsumoId && c.Quantidade == componente.Quantidade);
        }
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();

        Assert.Null(await repo.ObterPorIdAsync("produto-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_sku_retorna_o_produto()
    {
        var repo = CriarRepositorio();
        var produto = Produto.Criar(TenantA, "Cabo USB-C 1m", UnidadeDeMedida.UN, sku: "CABO-USBC-1M").Valor;
        await repo.SalvarAsync(produto);

        var lido = await repo.ObterPorSkuAsync(TenantA, "CABO-USBC-1M");

        Assert.NotNull(lido);
        Assert.Equal(produto.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_sku_de_outro_tenant_nao_retorna()
    {
        // Mesmo sku em dois tenants distintos não pode colidir — cada tenant tem seu próprio
        // catálogo (R1: businessId/tenantId é sagrado em toda query).
        var repo = CriarRepositorio();
        var produto = Produto.Criar(TenantA, "Cabo USB-C 1m", UnidadeDeMedida.UN, sku: "CABO-USBC-1M").Valor;
        await repo.SalvarAsync(produto);

        Assert.Null(await repo.ObterPorSkuAsync(TenantB, "CABO-USBC-1M"));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_inativar_reflete_o_novo_ativo()
    {
        var repo = CriarRepositorio();
        var produto = Produto.Criar(TenantA, "Produto descontinuado", UnidadeDeMedida.UN).Valor;
        await repo.SalvarAsync(produto);

        produto.Inativar();
        await repo.SalvarAsync(produto);

        var lido = await repo.ObterPorIdAsync(produto.Id);
        Assert.False(lido!.Ativo);
    }

    [Fact]
    public async Task Listar_retorna_apenas_produtos_do_tenant()
    {
        var repo = CriarRepositorio();
        var a = Produto.Criar(TenantA, "Produto A", UnidadeDeMedida.UN).Valor;
        var b = Produto.Criar(TenantB, "Produto B", UnidadeDeMedida.UN).Valor;

        await repo.SalvarAsync(a);
        await repo.SalvarAsync(b);

        var listaA = await repo.ListarAsync(TenantA);

        Assert.Single(listaA);
        Assert.Equal(a.Id, listaA[0].Id);
    }
}
