using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IReferenciaDevolucaoDocumentoFiscalRepository"/> —
/// roda 2× (InMemory + SQLite). Fecha a lacuna de cobertura do gap #5 (emissao-mapping.md §4.6/§11):
/// até aqui só <see cref="Sefaz.SefazApiGatewayTests"/> exercitava a leitura indiretamente; nenhum
/// teste cobria o repositório isoladamente (nem InMemory, nem SQLite).</summary>
public abstract class ReferenciaDevolucaoDocumentoFiscalRepositoryContractTests
{
    protected abstract IReferenciaDevolucaoDocumentoFiscalRepository CriarRepositorio();

    [Fact]
    public async Task Obter_sem_vinculo_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterRefNFeAsync("documento-1"));
    }

    [Fact]
    public async Task Vincular_e_obter_retorna_a_mesma_referencia()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync("documento-1", "35260112345678000195550010000000011000000099");

        Assert.Equal("35260112345678000195550010000000011000000099", await repo.ObterRefNFeAsync("documento-1"));
    }

    [Fact]
    public async Task Vincular_novamente_o_mesmo_documento_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync("documento-1", "35260112345678000195550010000000011000000001");
        await repo.VincularAsync("documento-1", "35260112345678000195550010000000011000000002");

        Assert.Equal("35260112345678000195550010000000011000000002", await repo.ObterRefNFeAsync("documento-1"));
    }

    [Fact]
    public async Task Documentos_diferentes_nao_se_confundem()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync("documento-1", "35260112345678000195550010000000011000000001");
        await repo.VincularAsync("documento-2", "35260112345678000195550010000000011000000002");

        Assert.Equal("35260112345678000195550010000000011000000001", await repo.ObterRefNFeAsync("documento-1"));
        Assert.Equal("35260112345678000195550010000000011000000002", await repo.ObterRefNFeAsync("documento-2"));
    }
}
