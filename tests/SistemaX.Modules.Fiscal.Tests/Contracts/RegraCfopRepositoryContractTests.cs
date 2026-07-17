using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IRegraCfopRepository"/> — roda 2× (InMemory +
/// SQLite). Cobre a camada "padrão-config" da cadeia emissão&gt;produto&gt;padrão-config
/// (docs/fiscal/arquitetura.md §2.3, decisão de Igor/ADR-0002) — inclusive o desempate por
/// especificidade (linha do tenant vence o default do sistema).</summary>
public abstract class RegraCfopRepositoryContractTests
{
    protected const string TenantA = "tenant-a";

    protected abstract IRegraCfopRepository CriarRepositorio();

    [Fact]
    public async Task Resolver_sem_nenhuma_regra_cadastrada_retorna_null()
    {
        var repo = CriarRepositorio();

        var resolvida = await repo.ResolverAsync(
            TenantA, TipoOperacaoFiscal.VendaMercadoria, ehInterestadual: false,
            destinatarioContribuinteIcms: true, NaturezaOperacaoProduto.RevendaDeTerceiros);

        Assert.Null(resolvida);
    }

    [Fact]
    public async Task Resolver_encontra_regra_default_do_sistema_sem_tenant()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new RegraCfop(
            TenantId: null, TipoOperacaoFiscal.VendaMercadoria, EhInterestadual: false,
            DestinatarioContribuinteIcms: true, NaturezaOperacaoProduto.RevendaDeTerceiros, Cfop: "5102"));

        var resolvida = await repo.ResolverAsync(
            TenantA, TipoOperacaoFiscal.VendaMercadoria, ehInterestadual: false,
            destinatarioContribuinteIcms: true, NaturezaOperacaoProduto.RevendaDeTerceiros);

        Assert.NotNull(resolvida);
        Assert.Equal("5102", resolvida!.Cfop);
    }

    [Fact]
    public async Task Natureza_do_produto_distingue_producao_propria_de_revenda_mesmo_com_a_mesma_operacao()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await repo.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.ProducaoPropria, "5101"));

        var revenda = await repo.ResolverAsync(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros);
        var producaoPropria = await repo.ResolverAsync(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.ProducaoPropria);

        Assert.Equal("5102", revenda!.Cfop);
        Assert.Equal("5101", producaoPropria!.Cfop);
    }

    [Fact]
    public async Task Regra_especifica_do_tenant_vence_o_default_do_sistema()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await repo.SalvarAsync(new RegraCfop(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5405"));

        var resolvida = await repo.ResolverAsync(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros);

        Assert.Equal("5405", resolvida!.Cfop);
    }

    [Fact]
    public async Task Listar_com_tenant_null_retorna_apenas_regras_default()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await repo.SalvarAsync(new RegraCfop(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5405"));

        var lista = await repo.ListarAsync(null);

        Assert.Single(lista);
        Assert.Null(lista[0].TenantId);
    }

    [Fact]
    public async Task Listar_com_tenant_retorna_defaults_e_especificas_do_tenant()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(new RegraCfop(null, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5102"));
        await repo.SalvarAsync(new RegraCfop(TenantA, TipoOperacaoFiscal.VendaMercadoria, false, true, NaturezaOperacaoProduto.RevendaDeTerceiros, "5405"));

        var lista = await repo.ListarAsync(TenantA);

        Assert.Equal(2, lista.Count);
    }
}
