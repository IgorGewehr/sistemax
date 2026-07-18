using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IConfiguracaoFiscalTenantRepository"/> — roda 2×
/// (InMemory + SQLite), mesmo molde de <c>DocumentoFiscalRepositoryContractTests</c>.</summary>
public abstract class ConfiguracaoFiscalTenantRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract IConfiguracaoFiscalTenantRepository CriarRepositorio();

    [Fact]
    public async Task Obter_de_tenant_sem_configuracao_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA));
    }

    [Fact]
    public async Task Salvar_e_obter_retorna_a_mesma_configuracao()
    {
        var repo = CriarRepositorio();
        var config = ConfiguracaoFiscalTenant.Criar(TenantA, RegimeTributario.SimplesNacional, "sp", "1", "1").Valor;

        await repo.SalvarAsync(config);
        var lida = await repo.ObterAsync(TenantA);

        Assert.NotNull(lida);
        Assert.Equal(TenantA, lida!.TenantId);
        Assert.Equal(RegimeTributario.SimplesNacional, lida.Regime);
        Assert.Equal("SP", lida.UfOrigem);
        Assert.Equal("1", lida.SerieNfce);
        Assert.Equal("1", lida.SerieNfe);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_tenant_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(ConfiguracaoFiscalTenant.Criar(TenantA, RegimeTributario.SimplesNacional, "sp").Valor);

        var atualizada = ConfiguracaoFiscalTenant.Criar(TenantA, RegimeTributario.LucroPresumido, "rj", "2", "3").Valor;
        await repo.SalvarAsync(atualizada);

        var lida = await repo.ObterAsync(TenantA);
        Assert.Equal(RegimeTributario.LucroPresumido, lida!.Regime);
        Assert.Equal("RJ", lida.UfOrigem);
        Assert.Equal("2", lida.SerieNfce);
        Assert.Equal("3", lida.SerieNfe);
    }

    [Fact]
    public async Task Tenants_diferentes_tem_configuracoes_independentes()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(ConfiguracaoFiscalTenant.Criar(TenantA, RegimeTributario.SimplesNacional, "sp").Valor);
        await repo.SalvarAsync(ConfiguracaoFiscalTenant.Criar(TenantB, RegimeTributario.LucroPresumido, "mg").Valor);

        Assert.Equal(RegimeTributario.SimplesNacional, (await repo.ObterAsync(TenantA))!.Regime);
        Assert.Equal(RegimeTributario.LucroPresumido, (await repo.ObterAsync(TenantB))!.Regime);
    }

    /// <summary>Passo 3(b) — CSC (Código de Segurança do Contribuinte) precisa sobreviver ao
    /// round-trip de persistência: sem ele a emissão de NFC-e nunca sai do papel (gap #2,
    /// docs/fiscal/emissao-mapping.md §11).</summary>
    [Fact]
    public async Task Csc_e_persistido_e_lido_no_round_trip()
    {
        var repo = CriarRepositorio();
        var config = ConfiguracaoFiscalTenant.Criar(
            TenantA, RegimeTributario.SimplesNacional, "sp", cscId: "000001", cscToken: "segredo-csc").Valor;

        await repo.SalvarAsync(config);
        var lida = await repo.ObterAsync(TenantA);

        Assert.Equal("000001", lida!.CscId);
        Assert.Equal("segredo-csc", lida.CscToken);
    }

    [Fact]
    public async Task Csc_ausente_persiste_como_null()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(ConfiguracaoFiscalTenant.Criar(TenantA, RegimeTributario.SimplesNacional, "sp").Valor);

        var lida = await repo.ObterAsync(TenantA);

        Assert.Null(lida!.CscId);
        Assert.Null(lida.CscToken);
    }
}
