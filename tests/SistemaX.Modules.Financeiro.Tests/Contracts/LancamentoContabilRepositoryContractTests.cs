using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="ILancamentoContabilRepository"/>. Sem caso de "mutar e
/// resalvar" — <see cref="LancamentoContabil"/> é IMUTÁVEL por construção; em vez disso, cobrimos
/// que salvar o MESMO lançamento duas vezes (replay) não duplica partidas (insert-only idempotente).
/// </summary>
public abstract class LancamentoContabilRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract ILancamentoContabilRepository CriarRepositorio();

    private static LancamentoContabil CriarLancamento(string businessId, string origemId, Money valor, DateTimeOffset data)
    {
        var partidas = new[]
        {
            PartidaContabil.Debito("conta-caixa", valor),
            PartidaContabil.Credito("conta-receita", valor)
        };
        return LancamentoContabil.Criar(businessId, data, "Lançamento de teste", new OrigemLancamento("vendas", "venda", origemId), partidas).Valor;
    }

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_o_mesmo_lancamento_com_partidas()
    {
        var repo = CriarRepositorio();
        var lancamento = CriarLancamento(BusinessA, "origem-1", Money.DeReais(500), DateTimeOffset.UtcNow);

        await repo.SalvarAsync(lancamento);
        var lido = await repo.ObterPorIdAsync(lancamento.Id);

        Assert.NotNull(lido);
        Assert.Equal(lancamento.Id, lido!.Id);
        Assert.Equal(lancamento.BusinessId, lido.BusinessId);
        Assert.Equal(lancamento.Data, lido.Data);
        Assert.Equal(lancamento.Descricao, lido.Descricao);
        Assert.Equal(lancamento.Origem, lido.Origem);
        Assert.Equal(lancamento.ReversalOfId, lido.ReversalOfId);
        Assert.Equal(lancamento.CriadoEm, lido.CriadoEm);

        Assert.Equal(lancamento.Partidas.Count, lido.Partidas.Count);
        for (var i = 0; i < lancamento.Partidas.Count; i++)
        {
            Assert.Equal(lancamento.Partidas[i].ContaContabilId, lido.Partidas[i].ContaContabilId);
            Assert.Equal(lancamento.Partidas[i].Natureza, lido.Partidas[i].Natureza);
            Assert.Equal(lancamento.Partidas[i].Valor, lido.Partidas[i].Valor);
        }
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync("lancamento-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_origem_retorna_o_lancamento()
    {
        var repo = CriarRepositorio();
        var lancamento = CriarLancamento(BusinessA, "origem-2", Money.DeReais(200), DateTimeOffset.UtcNow);
        await repo.SalvarAsync(lancamento);

        var lido = await repo.BuscarPorOrigemAsync(BusinessA, lancamento.Origem.Chave);

        Assert.NotNull(lido);
        Assert.Equal(lancamento.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_origem_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var lancamento = CriarLancamento(BusinessA, "origem-3", Money.DeReais(200), DateTimeOffset.UtcNow);
        await repo.SalvarAsync(lancamento);

        Assert.Null(await repo.BuscarPorOrigemAsync(BusinessB, lancamento.Origem.Chave));
    }

    [Fact]
    public async Task ListarPorPeriodoAsync_retorna_apenas_lancamentos_no_intervalo()
    {
        var repo = CriarRepositorio();
        var dentro = CriarLancamento(BusinessA, "origem-4", Money.DeReais(10), new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var fora = CriarLancamento(BusinessA, "origem-5", Money.DeReais(10), new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await repo.SalvarAsync(dentro);
        await repo.SalvarAsync(fora);

        var lista = await repo.ListarPorPeriodoAsync(
            BusinessA, new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero));

        Assert.Single(lista);
        Assert.Equal(dentro.Id, lista[0].Id);
    }

    [Fact]
    public async Task Salvar_o_mesmo_lancamento_duas_vezes_nao_duplica_partidas()
    {
        var repo = CriarRepositorio();
        var lancamento = CriarLancamento(BusinessA, "origem-6", Money.DeReais(300), DateTimeOffset.UtcNow);

        await repo.SalvarAsync(lancamento);
        await repo.SalvarAsync(lancamento); // replay do mesmo evento — insert-only deve ser idempotente

        var lido = await repo.ObterPorIdAsync(lancamento.Id);
        Assert.Equal(lancamento.Partidas.Count, lido!.Partidas.Count);
    }
}
