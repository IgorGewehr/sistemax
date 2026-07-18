using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Configuracao;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IConfiguracaoFinanceiraTenantRepository"/> — roda 2×
/// (InMemory + SQLite), espelho de <c>ConfiguracaoFiscalTenantRepositoryContractTests</c> (Fiscal).</summary>
public abstract class ConfiguracaoFinanceiraTenantRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract IConfiguracaoFinanceiraTenantRepository CriarRepositorio();

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
        var config = ConfiguracaoFinanceiraTenant.Criar(TenantA, analisePorProjetoAtiva: true, custoHoraPadraoCentavos: 5000, tempoEntraNoDre: false).Valor;

        await repo.SalvarAsync(config);
        var lida = await repo.ObterAsync(TenantA);

        Assert.NotNull(lida);
        Assert.Equal(TenantA, lida!.TenantId);
        Assert.True(lida.AnalisePorProjetoAtiva);
        Assert.Equal(5000, lida.CustoHoraPadraoCentavos);
        Assert.False(lida.TempoEntraNoDre);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_tenant_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(ConfiguracaoFinanceiraTenant.Padrao(TenantA));

        var ligada = ConfiguracaoFinanceiraTenant.Criar(TenantA, analisePorProjetoAtiva: true).Valor;
        await repo.SalvarAsync(ligada);

        var lida = await repo.ObterAsync(TenantA);
        Assert.True(lida!.AnalisePorProjetoAtiva);
    }

    [Fact]
    public async Task Tenants_diferentes_tem_configuracoes_independentes()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(TenantA, analisePorProjetoAtiva: true).Valor);
        await repo.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(TenantB, analisePorProjetoAtiva: false).Valor);

        Assert.True((await repo.ObterAsync(TenantA))!.AnalisePorProjetoAtiva);
        Assert.False((await repo.ObterAsync(TenantB))!.AnalisePorProjetoAtiva);
    }
}
