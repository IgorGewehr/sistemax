using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IDadosFiscaisProdutoCacheRepository"/> — roda 2×
/// (InMemory + SQLite). Esta cópia local é o que <see cref="Application.Cfop.ResolvedorDeCfop"/>
/// consulta no nível "produto" da cadeia emissão&gt;produto&gt;padrão-config
/// (docs/fiscal/arquitetura.md §2.3/§4).</summary>
public abstract class DadosFiscaisProdutoCacheRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract IDadosFiscaisProdutoCacheRepository CriarRepositorio();

    [Fact]
    public async Task Obter_de_produto_sem_cache_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, "produto-1"));
    }

    [Fact]
    public async Task Salvar_e_obter_retorna_os_mesmos_dados()
    {
        var repo = CriarRepositorio();
        var dados = new DadosFiscaisProdutoCache(TenantA, "produto-1", "12345678", "CEST123", NaturezaOperacaoProduto.ProducaoPropria, "5101");

        await repo.SalvarAsync(dados);
        var lido = await repo.ObterAsync(TenantA, "produto-1");

        Assert.NotNull(lido);
        Assert.Equal("12345678", lido!.Ncm);
        Assert.Equal("CEST123", lido.Cest);
        Assert.Equal(NaturezaOperacaoProduto.ProducaoPropria, lido.NaturezaOperacao);
        Assert.Equal("5101", lido.CfopOverride);
    }

    [Fact]
    public async Task Salvar_com_campos_opcionais_nulos_persiste_nulos_corretamente()
    {
        var repo = CriarRepositorio();
        var dados = new DadosFiscaisProdutoCache(TenantA, "produto-2", null, null, NaturezaOperacaoProduto.RevendaDeTerceiros, null);

        await repo.SalvarAsync(dados);
        var lido = await repo.ObterAsync(TenantA, "produto-2");

        Assert.NotNull(lido);
        Assert.Null(lido!.Ncm);
        Assert.Null(lido.Cest);
        Assert.Null(lido.CfopOverride);
        Assert.Equal(NaturezaOperacaoProduto.RevendaDeTerceiros, lido.NaturezaOperacao);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_produto_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new DadosFiscaisProdutoCache(TenantA, "produto-1", "11111111", null, NaturezaOperacaoProduto.RevendaDeTerceiros, null));
        await repo.SalvarAsync(new DadosFiscaisProdutoCache(TenantA, "produto-1", "22222222", "CEST9", NaturezaOperacaoProduto.ImportacaoPropria, "6102"));

        var lido = await repo.ObterAsync(TenantA, "produto-1");
        Assert.Equal("22222222", lido!.Ncm);
        Assert.Equal("CEST9", lido.Cest);
        Assert.Equal(NaturezaOperacaoProduto.ImportacaoPropria, lido.NaturezaOperacao);
        Assert.Equal("6102", lido.CfopOverride);
    }

    [Fact]
    public async Task Salvar_e_obter_com_gtin_e_unidade_comercial_preserva_ambos()
    {
        var repo = CriarRepositorio();
        var dados = new DadosFiscaisProdutoCache(
            TenantA, "produto-3", "12345678", null, NaturezaOperacaoProduto.RevendaDeTerceiros, null,
            Gtin: "7891234567895", UnidadeComercial: "CX");

        await repo.SalvarAsync(dados);
        var lido = await repo.ObterAsync(TenantA, "produto-3");

        Assert.NotNull(lido);
        Assert.Equal("7891234567895", lido!.Gtin);
        Assert.Equal("CX", lido.UnidadeComercial);
    }

    [Fact]
    public async Task Salvar_sem_gtin_e_unidade_comercial_persiste_nulos()
    {
        var repo = CriarRepositorio();
        var dados = new DadosFiscaisProdutoCache(TenantA, "produto-4", null, null, NaturezaOperacaoProduto.RevendaDeTerceiros, null);

        await repo.SalvarAsync(dados);
        var lido = await repo.ObterAsync(TenantA, "produto-4");

        Assert.NotNull(lido);
        Assert.Null(lido!.Gtin);
        Assert.Null(lido.UnidadeComercial);
    }

    [Fact]
    public async Task Mesmo_produto_id_em_tenants_diferentes_nao_se_confunde()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new DadosFiscaisProdutoCache(TenantA, "produto-1", "11111111", null, NaturezaOperacaoProduto.RevendaDeTerceiros, null));
        await repo.SalvarAsync(new DadosFiscaisProdutoCache(TenantB, "produto-1", "99999999", null, NaturezaOperacaoProduto.ProducaoPropria, null));

        Assert.Equal("11111111", (await repo.ObterAsync(TenantA, "produto-1"))!.Ncm);
        Assert.Equal("99999999", (await repo.ObterAsync(TenantB, "produto-1"))!.Ncm);
    }
}
