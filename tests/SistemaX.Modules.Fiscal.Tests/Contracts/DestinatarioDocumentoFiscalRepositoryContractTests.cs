using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IDestinatarioDocumentoFiscalRepository"/> (gap #1,
/// emissao-mapping.md §4.2/§11) — roda 2× (InMemory + SQLite).</summary>
public abstract class DestinatarioDocumentoFiscalRepositoryContractTests
{
    protected const string DocA = "documento-a";
    protected const string DocB = "documento-b";

    protected abstract IDestinatarioDocumentoFiscalRepository CriarRepositorio();

    private static DestinatarioDocumentoFiscal ComEndereco(string nome = "Cliente Teste") => new(
        Cnpj: null, Cpf: "12345678900", Nome: nome, Email: "cliente@teste.com", InscricaoEstadual: null,
        Endereco: new EnderecoDestinatarioFiscal("Rua A", "10", null, "Centro", "3550308", "São Paulo", "SP", "01000000"));

    [Fact]
    public async Task Obter_de_documento_sem_destinatario_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorDocumentoAsync(DocA));
    }

    [Fact]
    public async Task Vincular_e_obter_retorna_o_mesmo_destinatario_com_endereco()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync(DocA, ComEndereco());

        var lido = await repo.ObterPorDocumentoAsync(DocA);

        Assert.NotNull(lido);
        Assert.Equal("Cliente Teste", lido!.Nome);
        Assert.Equal("12345678900", lido.Cpf);
        Assert.Null(lido.Cnpj);
        Assert.NotNull(lido.Endereco);
        Assert.Equal("Rua A", lido.Endereco!.Logradouro);
        Assert.Equal("SP", lido.Endereco.Uf);
    }

    [Fact]
    public async Task Vincular_sem_endereco_persiste_destinatario_com_endereco_nulo()
    {
        var repo = CriarRepositorio();
        var destinatario = new DestinatarioDocumentoFiscal(null, null, "Consumidor Final", null, null, Endereco: null);
        await repo.VincularAsync(DocA, destinatario);

        var lido = await repo.ObterPorDocumentoAsync(DocA);

        Assert.NotNull(lido);
        Assert.Equal("Consumidor Final", lido!.Nome);
        Assert.Null(lido.Endereco);
    }

    [Fact]
    public async Task Vincular_novamente_o_mesmo_documento_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync(DocA, ComEndereco("Nome Antigo"));
        await repo.VincularAsync(DocA, ComEndereco("Nome Novo"));

        var lido = await repo.ObterPorDocumentoAsync(DocA);
        Assert.Equal("Nome Novo", lido!.Nome);
    }

    [Fact]
    public async Task Documentos_diferentes_tem_destinatarios_independentes()
    {
        var repo = CriarRepositorio();
        await repo.VincularAsync(DocA, ComEndereco("Cliente A"));
        await repo.VincularAsync(DocB, ComEndereco("Cliente B"));

        Assert.Equal("Cliente A", (await repo.ObterPorDocumentoAsync(DocA))!.Nome);
        Assert.Equal("Cliente B", (await repo.ObterPorDocumentoAsync(DocB))!.Nome);
    }
}
