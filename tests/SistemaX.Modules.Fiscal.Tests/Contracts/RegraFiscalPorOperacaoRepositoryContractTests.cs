using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IRegraFiscalPorOperacaoRepository"/> — roda 2×
/// (InMemory + SQLite). Cobre a matriz de decisão de CSOSN/CST (docs/fiscal/arquitetura.md §2.3)
/// e o desempate por especificidade (UfDestino exata vence "qualquer"; tenant vence default).</summary>
public abstract class RegraFiscalPorOperacaoRepositoryContractTests
{
    protected const string TenantA = "tenant-a";

    protected abstract IRegraFiscalPorOperacaoRepository CriarRepositorio();

    private static RegraFiscalPorOperacao RegraDefault(string? tenantId, string? ufDestino, string codigo) => new(
        TenantId: tenantId,
        Regime: RegimeTributario.SimplesNacional,
        TipoOperacao: TipoOperacaoFiscal.VendaMercadoria,
        UfOrigem: "SP",
        UfDestino: ufDestino,
        IndicadorSt: false,
        SituacaoIcms: SituacaoTributariaIcms.ParaCsosn(RegimeTributario.SimplesNacional, codigo).Valor,
        AliquotaInterna: null,
        AliquotaInterestadual: null);

    [Fact]
    public async Task Resolver_sem_nenhuma_regra_cadastrada_retorna_null()
    {
        var repo = CriarRepositorio();
        var resolvida = await repo.ResolverAsync(TenantA, RegimeTributario.SimplesNacional, TipoOperacaoFiscal.VendaMercadoria, "SP", "SP", false);
        Assert.Null(resolvida);
    }

    [Fact]
    public async Task Resolver_encontra_regra_default_qualquer_destino()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));

        var resolvida = await repo.ResolverAsync(TenantA, RegimeTributario.SimplesNacional, TipoOperacaoFiscal.VendaMercadoria, "SP", "RJ", false);

        Assert.NotNull(resolvida);
        Assert.Equal("102", resolvida!.SituacaoIcms.Codigo);
    }

    [Fact]
    public async Task Regra_com_uf_destino_especifico_vence_regra_de_qualquer_destino()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));
        await repo.SalvarAsync(RegraDefault(null, "RJ", "500"));

        var paraRj = await repo.ResolverAsync(TenantA, RegimeTributario.SimplesNacional, TipoOperacaoFiscal.VendaMercadoria, "SP", "RJ", false);
        var paraMg = await repo.ResolverAsync(TenantA, RegimeTributario.SimplesNacional, TipoOperacaoFiscal.VendaMercadoria, "SP", "MG", false);

        Assert.Equal("500", paraRj!.SituacaoIcms.Codigo);
        Assert.Equal("102", paraMg!.SituacaoIcms.Codigo);
    }

    [Fact]
    public async Task Regra_especifica_do_tenant_vence_o_default_do_sistema()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));
        await repo.SalvarAsync(RegraDefault(TenantA, null, "900"));

        var resolvida = await repo.ResolverAsync(TenantA, RegimeTributario.SimplesNacional, TipoOperacaoFiscal.VendaMercadoria, "SP", "RJ", false);

        Assert.Equal("900", resolvida!.SituacaoIcms.Codigo);
    }

    [Fact]
    public async Task Listar_com_tenant_null_retorna_apenas_regras_default()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));
        await repo.SalvarAsync(RegraDefault(TenantA, null, "900"));

        var lista = await repo.ListarAsync(null);

        Assert.Single(lista);
        Assert.Null(lista[0].TenantId);
    }

    [Fact]
    public async Task Listar_com_tenant_retorna_defaults_e_especificas_do_tenant()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));
        await repo.SalvarAsync(RegraDefault(TenantA, null, "900"));

        var lista = await repo.ListarAsync(TenantA);

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public async Task Salvar_novamente_a_mesma_chave_natural_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(RegraDefault(null, null, "102"));
        await repo.SalvarAsync(RegraDefault(null, null, "900"));

        var lista = await repo.ListarAsync(null);

        Assert.Single(lista);
        Assert.Equal("900", lista[0].SituacaoIcms.Codigo);
    }
}
