using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Tests.Contracts;

/// <summary>Contract test do port <see cref="ITributacaoProdutoRepository"/> — roda 2× (InMemory +
/// SQLite). Cobre o override por SKU (docs/fiscal/arquitetura.md §2.5) que fecha o gap central do
/// gestao-raiz: um produto com benefício fiscal individual precisa divergir da matriz sem editar
/// código.</summary>
public abstract class TributacaoProdutoRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";

    protected abstract ITributacaoProdutoRepository CriarRepositorio();

    [Fact]
    public async Task Obter_produto_sem_override_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, "produto-1"));
    }

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_override_com_motivo()
    {
        var repo = CriarRepositorio();
        var tributacao = TributacaoProduto.Criar(
            TenantA, "produto-1", motivo: "Benefício fiscal estadual específico deste SKU",
            situacaoTributariaIcms: "060", aliquotaIcms: Percentual.DePorcentagem(12),
            origem: OrigemMercadoria.Nacional).Valor;

        await repo.SalvarAsync(tributacao);
        var lido = await repo.ObterAsync(TenantA, "produto-1");

        Assert.NotNull(lido);
        Assert.Equal("060", lido!.SituacaoTributariaIcmsOverride);
        Assert.Equal(Percentual.DePorcentagem(12), lido.AliquotaIcmsOverride);
        Assert.Equal(OrigemMercadoria.Nacional, lido.OrigemOverride);
        Assert.Equal("Benefício fiscal estadual específico deste SKU", lido.Motivo);
    }

    [Fact]
    public async Task Salvar_sem_nenhum_campo_de_override_persiste_todos_nulos()
    {
        var repo = CriarRepositorio();
        var semOverride = TributacaoProduto.Criar(TenantA, "produto-2", motivo: "").Valor;

        await repo.SalvarAsync(semOverride);
        var lido = await repo.ObterAsync(TenantA, "produto-2");

        Assert.NotNull(lido);
        Assert.Null(lido!.SituacaoTributariaIcmsOverride);
        Assert.Null(lido.OrigemOverride);
        Assert.Null(lido.AliquotaIcmsOverride);
    }

    [Fact]
    public async Task Salvar_novamente_o_mesmo_produto_atualiza_em_vez_de_duplicar()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(TributacaoProduto.Criar(
            TenantA, "produto-1", motivo: "Motivo inicial", situacaoTributariaIcms: "060").Valor);

        await repo.SalvarAsync(TributacaoProduto.Criar(
            TenantA, "produto-1", motivo: "Motivo revisado", situacaoTributariaIcms: "500").Valor);

        var lido = await repo.ObterAsync(TenantA, "produto-1");
        Assert.Equal("500", lido!.SituacaoTributariaIcmsOverride);
        Assert.Equal("Motivo revisado", lido.Motivo);
    }

    [Fact]
    public async Task Mesmo_produto_id_em_tenants_diferentes_nao_se_confunde()
    {
        var repo = CriarRepositorio();
        await repo.SalvarAsync(TributacaoProduto.Criar(
            TenantA, "produto-1", motivo: "A", situacaoTributariaIcms: "060").Valor);
        await repo.SalvarAsync(TributacaoProduto.Criar(
            TenantB, "produto-1", motivo: "B", situacaoTributariaIcms: "500").Valor);

        Assert.Equal("060", (await repo.ObterAsync(TenantA, "produto-1"))!.SituacaoTributariaIcmsOverride);
        Assert.Equal("500", (await repo.ObterAsync(TenantB, "produto-1"))!.SituacaoTributariaIcmsOverride);
    }
}
