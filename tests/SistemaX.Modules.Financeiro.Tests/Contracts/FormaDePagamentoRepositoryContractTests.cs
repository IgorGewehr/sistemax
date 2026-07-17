using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFormaDePagamentoRepository"/> — roda o MESMO conjunto
/// de casos contra <c>InMemoryFormaDePagamentoRepository</c> e <c>SqliteFormaDePagamentoRepository</c>.
/// <see cref="ObterPorNomeAsync_case_insensitive"/> prova o caminho que <c>FatoRecebiveisProjection</c>
/// depende para resolver MDR/lag — o motivo de este repositório existir.</summary>
public abstract class FormaDePagamentoRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IFormaDePagamentoRepository CriarRepositorio();

    private static FormaDePagamento CriarForma(
        string businessId, string nome, TipoFormaPagamento tipo = TipoFormaPagamento.Credito,
        decimal taxa = 0.0349m, int lagDias = 30, string? contaLiquidacaoId = null)
        => FormaDePagamento.Criar(businessId, nome, tipo, taxa, lagDias, contaLiquidacaoId).Valor;

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_forma()
    {
        var repo = CriarRepositorio();
        var forma = CriarForma(BusinessA, "credito", contaLiquidacaoId: "conta-1");

        await repo.SalvarAsync(forma);
        var lida = await repo.ObterPorIdAsync(BusinessA, forma.Id);

        Assert.NotNull(lida);
        Assert.Equal(forma.Id, lida!.Id);
        Assert.Equal(forma.BusinessId, lida.BusinessId);
        Assert.Equal(forma.Nome, lida.Nome);
        Assert.Equal(forma.Tipo, lida.Tipo);
        Assert.Equal(forma.TaxaPercentual, lida.TaxaPercentual);
        Assert.Equal(forma.PrazoCompensacaoDias, lida.PrazoCompensacaoDias);
        Assert.Equal(forma.ContaLiquidacaoId, lida.ContaLiquidacaoId);
        Assert.Equal(forma.Ativo, lida.Ativo);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, "forma-que-nao-existe"));
    }

    [Fact]
    public async Task ObterPorNomeAsync_case_insensitive()
    {
        var repo = CriarRepositorio();
        var forma = CriarForma(BusinessA, "Crédito", tipo: TipoFormaPagamento.Credito, taxa: 0.0349m, lagDias: 30);
        await repo.SalvarAsync(forma);

        var lida = await repo.ObterPorNomeAsync(BusinessA, "CRÉDITO");

        Assert.NotNull(lida);
        Assert.Equal(forma.Id, lida!.Id);
    }

    [Fact]
    public async Task ObterPorNomeAsync_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var forma = CriarForma(BusinessA, "pix");
        await repo.SalvarAsync(forma);

        Assert.Null(await repo.ObterPorNomeAsync(BusinessB, "pix"));
    }

    [Fact]
    public async Task ObterPorNomeAsync_nome_nao_cadastrado_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorNomeAsync(BusinessA, "carteira-digital-nova"));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_inativar_reflete_o_novo_estado()
    {
        var repo = CriarRepositorio();
        var forma = CriarForma(BusinessA, "boleto");
        await repo.SalvarAsync(forma);

        forma.Inativar();
        await repo.SalvarAsync(forma);

        var lida = await repo.ObterPorIdAsync(BusinessA, forma.Id);
        Assert.False(lida!.Ativo);
    }

    [Fact]
    public async Task ListarAsync_retorna_apenas_do_business_pedido()
    {
        var repo = CriarRepositorio();
        var daLojaA = CriarForma(BusinessA, "pix");
        var daLojaB = CriarForma(BusinessB, "pix");

        await repo.SalvarAsync(daLojaA);
        await repo.SalvarAsync(daLojaB);

        var lista = await repo.ListarAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(daLojaA.Id, lista[0].Id);
    }

    [Fact]
    public async Task ListarAsync_apenasAtivas_filtra_as_inativas()
    {
        var repo = CriarRepositorio();
        var ativa = CriarForma(BusinessA, "dinheiro");
        var inativa = CriarForma(BusinessA, "cheque");
        inativa.Inativar();

        await repo.SalvarAsync(ativa);
        await repo.SalvarAsync(inativa);

        var lista = await repo.ListarAsync(BusinessA, apenasAtivas: true);

        Assert.Single(lista);
        Assert.Equal(ativa.Id, lista[0].Id);
    }
}
