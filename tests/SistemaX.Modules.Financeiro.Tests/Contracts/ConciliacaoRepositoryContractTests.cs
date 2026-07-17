using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IConciliacaoRepository"/>. O port não expõe
/// <c>businessId</c> em <see cref="IConciliacaoRepository.BuscarPorParAsync"/> — por isso o
/// caso negativo é "par diferente não retorna" em vez do padrão cross-tenant usado nos demais
/// ports.
/// </summary>
public abstract class ConciliacaoRepositoryContractTests
{
    protected abstract IConciliacaoRepository CriarRepositorio();

    [Fact]
    public async Task Salvar_e_buscar_por_id_retorna_a_mesma_conciliacao()
    {
        var repo = CriarRepositorio();
        var conciliacao = Conciliacao.Criar("biz-a", "movimento-1", "extrato-1");

        await repo.SalvarAsync(conciliacao);
        var lido = await repo.ObterPorIdAsync(conciliacao.Id);

        Assert.NotNull(lido);
        Assert.Equal(conciliacao.Id, lido!.Id);
        Assert.Equal(conciliacao.BusinessId, lido.BusinessId);
        Assert.Equal(conciliacao.MovimentoFinanceiroId, lido.MovimentoFinanceiroId);
        Assert.Equal(conciliacao.ExtratoBancarioItemId, lido.ExtratoBancarioItemId);
        Assert.Equal(conciliacao.Status, lido.Status);
        Assert.Equal(conciliacao.ConciliadoEm, lido.ConciliadoEm);
    }

    [Fact]
    public async Task Buscar_por_id_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync("conciliacao-que-nao-existe"));
    }

    [Fact]
    public async Task Salvar_e_buscar_por_par_retorna_a_conciliacao()
    {
        var repo = CriarRepositorio();
        var conciliacao = Conciliacao.Criar("biz-a", "movimento-2", "extrato-2");
        await repo.SalvarAsync(conciliacao);

        var lido = await repo.BuscarPorParAsync("movimento-2", "extrato-2");

        Assert.NotNull(lido);
        Assert.Equal(conciliacao.Id, lido!.Id);
    }

    [Fact]
    public async Task Buscar_por_par_diferente_nao_retorna()
    {
        var repo = CriarRepositorio();
        var conciliacao = Conciliacao.Criar("biz-a", "movimento-3", "extrato-3");
        await repo.SalvarAsync(conciliacao);

        Assert.Null(await repo.BuscarPorParAsync("movimento-3", "extrato-outro"));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_confirmar_reflete_novo_estado()
    {
        var repo = CriarRepositorio();
        var conciliacao = Conciliacao.Criar("biz-a", "movimento-4", "extrato-4");
        await repo.SalvarAsync(conciliacao);

        var agora = DateTimeOffset.UtcNow;
        conciliacao.Confirmar(automatico: true, agora);
        await repo.SalvarAsync(conciliacao);

        var lido = await repo.ObterPorIdAsync(conciliacao.Id);
        Assert.Equal(StatusConciliacao.ConciliadoAuto, lido!.Status);
        Assert.Equal(agora, lido.ConciliadoEm);
    }

    [Fact]
    public async Task ListarPorBusinessIdAsync_devolve_so_as_conciliacoes_do_tenant()
    {
        var repo = CriarRepositorio();
        var daBizA1 = Conciliacao.Criar("biz-a", "movimento-5", "extrato-5");
        var daBizA2 = Conciliacao.Criar("biz-a", "movimento-6", "extrato-6");
        var daBizB = Conciliacao.Criar("biz-b", "movimento-7", "extrato-7");
        await repo.SalvarAsync(daBizA1);
        await repo.SalvarAsync(daBizA2);
        await repo.SalvarAsync(daBizB);

        var resultado = await repo.ListarPorBusinessIdAsync("biz-a");

        Assert.Equal(2, resultado.Count);
        Assert.All(resultado, c => Assert.Equal("biz-a", c.BusinessId));
        Assert.Contains(resultado, c => c.Id == daBizA1.Id);
        Assert.Contains(resultado, c => c.Id == daBizA2.Id);
    }

    [Fact]
    public async Task ListarPorBusinessIdAsync_sem_conciliacoes_devolve_lista_vazia()
    {
        var repo = CriarRepositorio();
        Assert.Empty(await repo.ListarPorBusinessIdAsync("biz-sem-nenhuma"));
    }
}
