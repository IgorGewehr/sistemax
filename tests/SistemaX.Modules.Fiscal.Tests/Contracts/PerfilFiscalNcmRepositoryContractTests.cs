using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IPerfilFiscalNcmRepository"/> — roda 2× (InMemory +
/// SQLite), mesmo molde de <c>DocumentoFiscalRepositoryContractTests</c>.</summary>
public abstract class PerfilFiscalNcmRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    protected const string Ncm = "12345678";

    protected abstract IPerfilFiscalNcmRepository CriarRepositorio();

    private static PerfilFiscalNCM CriarPerfil(string tenantId, RegimeTributario regime, string ncm) => PerfilFiscalNCM.Criar(
        tenantId, regime, ncm, OrigemMercadoria.Nacional, exigeIcmsSt: true, cest: "CEST1",
        aliquotaIpi: Percentual.DePorcentagem(5), cstOuCsosnPisCofins: "01",
        aliquotaPis: Percentual.DePorcentagem(1.65m), aliquotaCofins: Percentual.DePorcentagem(7.6m)).Valor;

    [Fact]
    public async Task Obter_perfil_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, RegimeTributario.SimplesNacional, Ncm));
    }

    [Fact]
    public async Task Salvar_e_obter_pela_chave_composta_tenant_regime_ncm()
    {
        var repo = CriarRepositorio();
        var perfil = CriarPerfil(TenantA, RegimeTributario.SimplesNacional, Ncm);

        await repo.SalvarAsync(perfil);
        var lido = await repo.ObterAsync(TenantA, RegimeTributario.SimplesNacional, Ncm);

        Assert.NotNull(lido);
        Assert.Equal(Ncm, lido!.Ncm);
        Assert.Equal(OrigemMercadoria.Nacional, lido.Origem);
        Assert.True(lido.ExigeIcmsSt);
        Assert.Equal("CEST1", lido.Cest);
        Assert.Equal("01", lido.CstOuCsosnPisCofins);
    }

    [Fact]
    public async Task Mesmo_ncm_em_regimes_diferentes_do_mesmo_tenant_sao_perfis_distintos()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(CriarPerfil(TenantA, RegimeTributario.SimplesNacional, Ncm));
        await repo.SalvarAsync(PerfilFiscalNCM.Criar(
            TenantA, RegimeTributario.LucroPresumido, Ncm, OrigemMercadoria.EstrangeiraImportacaoDireta,
            exigeIcmsSt: false, cest: null, aliquotaIpi: null, cstOuCsosnPisCofins: "99",
            aliquotaPis: null, aliquotaCofins: null).Valor);

        var simples = await repo.ObterAsync(TenantA, RegimeTributario.SimplesNacional, Ncm);
        var presumido = await repo.ObterAsync(TenantA, RegimeTributario.LucroPresumido, Ncm);

        Assert.Equal(OrigemMercadoria.Nacional, simples!.Origem);
        Assert.Equal(OrigemMercadoria.EstrangeiraImportacaoDireta, presumido!.Origem);
    }

    [Fact]
    public async Task Salvar_novamente_a_mesma_chave_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(CriarPerfil(TenantA, RegimeTributario.SimplesNacional, Ncm));

        var atualizado = PerfilFiscalNCM.Criar(
            TenantA, RegimeTributario.SimplesNacional, Ncm, OrigemMercadoria.Nacional,
            exigeIcmsSt: false, cest: null, aliquotaIpi: null, cstOuCsosnPisCofins: "102",
            aliquotaPis: null, aliquotaCofins: null).Valor;
        await repo.SalvarAsync(atualizado);

        var lista = await repo.ListarAsync(TenantA, RegimeTributario.SimplesNacional);
        Assert.Single(lista);
        Assert.Equal("102", lista[0].CstOuCsosnPisCofins);
        Assert.False(lista[0].ExigeIcmsSt);
    }

    [Fact]
    public async Task Listar_retorna_apenas_perfis_do_tenant_e_regime_pedidos()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(CriarPerfil(TenantA, RegimeTributario.SimplesNacional, "11111111"));
        await repo.SalvarAsync(CriarPerfil(TenantA, RegimeTributario.SimplesNacional, "22222222"));
        await repo.SalvarAsync(CriarPerfil(TenantA, RegimeTributario.LucroPresumido, "33333333"));
        await repo.SalvarAsync(CriarPerfil(TenantB, RegimeTributario.SimplesNacional, "44444444"));

        var lista = await repo.ListarAsync(TenantA, RegimeTributario.SimplesNacional);

        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, p => p.Ncm == "11111111");
        Assert.Contains(lista, p => p.Ncm == "22222222");
    }
}
