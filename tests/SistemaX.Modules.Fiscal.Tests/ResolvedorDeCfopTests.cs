using SistemaX.Modules.Fiscal.Application.Cfop;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Cobre a cadeia de resolução de CFOP decidida por Igor (ADR-0002,
/// docs/fiscal/arquitetura.md §2.3): <c>emissão &gt; produto &gt; padrão-config</c>, nunca
/// hardcode em nenhum dos 3 níveis, e falha nomeada (nunca CFOP "chutado") quando nenhum resolve.
/// </summary>
public sealed class ResolvedorDeCfopTests
{
    private const string TenantId = "tenant-1";
    private const string ProdutoId = "produto-1";

    private static OperacaoFiscal OperacaoInterna() => new(
        TipoOperacaoFiscal.VendaMercadoria, "SP", "SP",
        DestinatarioConsumidorFinal: true, DestinatarioContribuinteIcms: false, OperacaoPresencial: true);

    private static (ResolvedorDeCfop Resolvedor, IDadosFiscaisProdutoCacheRepository Cache, IRegraCfopRepository Regras) CriarSut()
    {
        var cache = new InMemoryDadosFiscaisProdutoCacheRepository();
        var regras = new InMemoryRegraCfopRepository();
        return (new ResolvedorDeCfop(cache, regras), cache, regras);
    }

    [Fact]
    public async Task ResolverAsync_ComCfopDeEmissao_VenceProdutoEConfig()
    {
        var (resolvedor, cache, regras) = CriarSut();
        await cache.SalvarAsync(new DadosFiscaisProdutoCache(TenantId, ProdutoId, "12345678", null, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await regras.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, false, NaturezaOperacaoProduto.RevendaDeTerceiros, "5405"));

        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), ProdutoId, cfopDaEmissao: "5949");

        Assert.True(resultado.Sucesso);
        Assert.Equal("5949", resultado.Valor);
    }

    [Fact]
    public async Task ResolverAsync_SemCfopDeEmissao_UsaOverrideDoProduto_VenceConfig()
    {
        var (resolvedor, cache, regras) = CriarSut();
        await cache.SalvarAsync(new DadosFiscaisProdutoCache(TenantId, ProdutoId, "12345678", null, NaturezaOperacaoProduto.ProducaoPropria, "5101"));
        await regras.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, false, NaturezaOperacaoProduto.ProducaoPropria, "5401"));

        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), ProdutoId, cfopDaEmissao: null);

        Assert.True(resultado.Sucesso);
        Assert.Equal("5101", resultado.Valor);
    }

    [Fact]
    public async Task ResolverAsync_SemEmissaoESemOverrideDeProduto_UsaPadraoConfig()
    {
        var (resolvedor, cache, regras) = CriarSut();
        await cache.SalvarAsync(new DadosFiscaisProdutoCache(TenantId, ProdutoId, "12345678", null, NaturezaOperacaoProduto.RevendaDeTerceiros, CfopOverride: null));
        await regras.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, false, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));

        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), ProdutoId, cfopDaEmissao: null);

        Assert.True(resultado.Sucesso);
        Assert.Equal("5102", resultado.Valor);
    }

    [Fact]
    public async Task ResolverAsync_SemCacheDeProdutoNenhum_AssumeRevendaDeTerceiros_EResolvePeloConfig()
    {
        var (resolvedor, _, regras) = CriarSut();
        await regras.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, false, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));

        // Produto nunca cadastrado no cache — default de NaturezaOperacaoProduto é RevendaDeTerceiros
        // (nunca ProducaoPropria silencioso, que teria implicação tributária mais favorável).
        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), "produto-nunca-visto", cfopDaEmissao: null);

        Assert.True(resultado.Sucesso);
        Assert.Equal("5102", resultado.Valor);
    }

    [Fact]
    public async Task ResolverAsync_SemEmissaoSemProdutoSemConfig_FalhaComCodigoNomeado()
    {
        var (resolvedor, cache, _) = CriarSut();
        await cache.SalvarAsync(new DadosFiscaisProdutoCache(TenantId, ProdutoId, "12345678", null, NaturezaOperacaoProduto.RevendaDeTerceiros, CfopOverride: null));

        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), ProdutoId, cfopDaEmissao: null);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.cfop.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ResolverAsync_CfopDeEmissaoEmBranco_NaoConta_CaiParaOsNiveisSeguintes()
    {
        var (resolvedor, cache, regras) = CriarSut();
        await cache.SalvarAsync(new DadosFiscaisProdutoCache(TenantId, ProdutoId, "12345678", null, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await regras.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, false, NaturezaOperacaoProduto.RevendaDeTerceiros, "9999"));

        var resultado = await resolvedor.ResolverAsync(TenantId, OperacaoInterna(), ProdutoId, cfopDaEmissao: "   ");

        Assert.True(resultado.Sucesso);
        Assert.Equal("5102", resultado.Valor);
    }
}
