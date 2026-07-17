using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="ICadastroFiscalEmitenteRepository"/> (gap #4,
/// emissao-mapping.md §3/§11) — roda 2× (InMemory + SQLite), mesmo molde de
/// <c>ConfiguracaoFiscalTenantRepositoryContractTests</c>.</summary>
public abstract class CadastroFiscalEmitenteRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract ICadastroFiscalEmitenteRepository CriarRepositorio();

    private static CadastroFiscalEmitente Emitente(string tenantId, string razaoSocial = "Empresa Teste LTDA") => new(
        tenantId, "00000000000191", razaoSocial, "Empresa Teste", "123456789", null,
        "Rua Teste", "100", null, "Centro", "3550308", "São Paulo", "01000000", "1155550000");

    [Fact]
    public async Task Obter_de_tenant_sem_cadastro_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA));
    }

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_cadastro()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(Emitente(TenantA));

        var lido = await repo.ObterAsync(TenantA);

        Assert.NotNull(lido);
        Assert.Equal(TenantA, lido!.TenantId);
        Assert.Equal("00000000000191", lido.Cnpj);
        Assert.Equal("Empresa Teste LTDA", lido.RazaoSocial);
        Assert.Equal("Empresa Teste", lido.NomeFantasia);
        Assert.Equal("123456789", lido.InscricaoEstadual);
        Assert.Null(lido.InscricaoMunicipal);
        Assert.Equal("Rua Teste", lido.Logradouro);
        Assert.Equal("3550308", lido.CodigoMunicipio);
        Assert.Equal("1155550000", lido.Telefone);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_tenant_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(Emitente(TenantA, "Razao Antiga"));
        await repo.SalvarAsync(Emitente(TenantA, "Razao Nova"));

        var lido = await repo.ObterAsync(TenantA);
        Assert.Equal("Razao Nova", lido!.RazaoSocial);
    }

    [Fact]
    public async Task Tenants_diferentes_tem_cadastros_independentes()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(Emitente(TenantA, "Empresa A"));
        await repo.SalvarAsync(Emitente(TenantB, "Empresa B"));

        Assert.Equal("Empresa A", (await repo.ObterAsync(TenantA))!.RazaoSocial);
        Assert.Equal("Empresa B", (await repo.ObterAsync(TenantB))!.RazaoSocial);
    }
}
