using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="IDocumentoFiscalRepository"/> — roda 2× (InMemory +
/// SQLite), mesmo molde de <c>MovimentoFinanceiroRepositoryContractTests</c> do Financeiro.</summary>
public abstract class DocumentoFiscalRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract IDocumentoFiscalRepository CriarRepositorio();

    private static ItemDocumentoFiscal ItemComIcms(string produtoId = "produto-1") => new(
        produtoId, "Produto 1", "12345678", "CEST123", OrigemMercadoria.Nacional, "5102",
        new Quantidade(2000), new Money(1500), new Money(100),
        [new TributoResolvidoItem(TipoTributo.Icms, "102", new Money(2900), new Percentual(180_000), new Money(522))]);

    private static DocumentoFiscal CriarDocumentoComItem(string tenantId, string vendaId)
    {
        var doc = DocumentoFiscal.Abrir(tenantId, TipoDocumentoFiscal.NFCe, new SourceRef("vendas", vendaId));
        doc.AdicionarItemResolvido(ItemComIcms());
        return doc;
    }

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_o_mesmo_documento_com_itens_e_tributos()
    {
        var repo = CriarRepositorio();
        var doc = CriarDocumentoComItem(TenantA, "venda-1");

        await repo.SalvarAsync(doc);
        var lido = await repo.ObterPorIdAsync(doc.Id);

        Assert.NotNull(lido);
        Assert.Equal(doc.Id, lido!.Id);
        Assert.Equal(doc.TenantId, lido.TenantId);
        Assert.Equal(doc.Status, lido.Status);
        Assert.Single(lido.Itens);
        Assert.Equal(doc.Itens[0].ProdutoId, lido.Itens[0].ProdutoId);
        Assert.Equal(doc.Itens[0].Cfop, lido.Itens[0].Cfop);
        Assert.Single(lido.Itens[0].Tributos);
        Assert.Equal(doc.Itens[0].Tributos[0].Valor, lido.Itens[0].Tributos[0].Valor);
        Assert.Equal(doc.Itens[0].Tributos[0].SituacaoTributaria, lido.Itens[0].Tributos[0].SituacaoTributaria);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync("documento-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_por_origem_retorna_o_documento_idempotencia()
    {
        var repo = CriarRepositorio();
        var doc = CriarDocumentoComItem(TenantA, "venda-2");
        await repo.SalvarAsync(doc);

        var lido = await repo.ObterPorOrigemAsync(TenantA, doc.Origem.Chave);

        Assert.NotNull(lido);
        Assert.Equal(doc.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_origem_de_outro_tenant_nao_retorna()
    {
        var repo = CriarRepositorio();
        var doc = CriarDocumentoComItem(TenantA, "venda-3");
        await repo.SalvarAsync(doc);

        Assert.Null(await repo.ObterPorOrigemAsync(TenantB, doc.Origem.Chave));
    }

    [Fact]
    public async Task Salvar_apos_alocar_numero_persiste_status_e_numero_atualizados()
    {
        var repo = CriarRepositorio();
        var doc = CriarDocumentoComItem(TenantA, "venda-4");
        await repo.SalvarAsync(doc);

        doc.AlocarNumero("1", 42);
        await repo.SalvarAsync(doc);

        var lido = await repo.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.NumeroAlocado, lido!.Status);
        Assert.Equal("1", lido.Serie);
        Assert.Equal(42, lido.Numero);
    }

    [Fact]
    public async Task Salvar_apos_autorizacao_persiste_chave_e_protocolo()
    {
        var repo = CriarRepositorio();
        var doc = CriarDocumentoComItem(TenantA, "venda-5");
        await repo.SalvarAsync(doc);

        doc.AlocarNumero("1", 43);
        doc.RegistrarAutorizacao("chave-acesso-teste", "protocolo-teste", DateTimeOffset.UtcNow);
        await repo.SalvarAsync(doc);

        var lido = await repo.ObterPorIdAsync(doc.Id);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, lido!.Status);
        Assert.Equal("chave-acesso-teste", lido.ChaveDeAcesso);
        Assert.Equal("protocolo-teste", lido.Protocolo);
    }
}
