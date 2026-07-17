using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="ICertificadoDigitalRepository"/> (gap #2,
/// emissao-mapping.md §4.6/§11) — roda 2× (InMemory + SQLite).</summary>
public abstract class CertificadoDigitalRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract ICertificadoDigitalRepository CriarRepositorio();

    [Fact]
    public async Task Obter_de_tenant_sem_certificado_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA));
    }

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_certificado()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(TenantA, new CertificadoDigital("cGZ4LWZha2U=", "senha-fake"));

        var lido = await repo.ObterAsync(TenantA);

        Assert.NotNull(lido);
        Assert.Equal("cGZ4LWZha2U=", lido!.PfxBase64);
        Assert.Equal("senha-fake", lido.Senha);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_tenant_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(TenantA, new CertificadoDigital("pfx-antigo", "senha-antiga"));
        await repo.SalvarAsync(TenantA, new CertificadoDigital("pfx-novo", "senha-nova"));

        var lido = await repo.ObterAsync(TenantA);
        Assert.Equal("pfx-novo", lido!.PfxBase64);
        Assert.Equal("senha-nova", lido.Senha);
    }

    [Fact]
    public async Task Tenants_diferentes_tem_certificados_independentes()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(TenantA, new CertificadoDigital("pfx-a", "senha-a"));
        await repo.SalvarAsync(TenantB, new CertificadoDigital("pfx-b", "senha-b"));

        Assert.Equal("pfx-a", (await repo.ObterAsync(TenantA))!.PfxBase64);
        Assert.Equal("pfx-b", (await repo.ObterAsync(TenantB))!.PfxBase64);
    }
}
