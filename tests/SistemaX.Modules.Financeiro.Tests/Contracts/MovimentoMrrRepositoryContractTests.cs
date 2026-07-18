using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IMovimentoMrrRepository"/> (P1-4) — exercitado por AMBOS os
/// adapters. Append-only por natureza: sem caso de "mutar e resalvar" (mesmo racional de
/// <c>LancamentoContabilRepositoryContractTests</c>).
/// </summary>
public abstract class MovimentoMrrRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IMovimentoMrrRepository CriarRepositorio();

    [Fact]
    public async Task Registrar_e_listar_retorna_o_movimento_persistido()
    {
        var repo = CriarRepositorio();
        var movimento = MovimentoMrr.Novo(BusinessA, "assinatura-1", "servico-1", 50000, new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero));

        await repo.RegistrarAsync(movimento);
        var lista = await repo.ListarAsync(BusinessA);

        var lido = Assert.Single(lista);
        Assert.Equal(movimento.Id, lido.Id);
        Assert.Equal(movimento.BusinessId, lido.BusinessId);
        Assert.Equal(movimento.AssinaturaId, lido.AssinaturaId);
        Assert.Equal(movimento.ServicoId, lido.ServicoId);
        Assert.Equal(movimento.Tipo, lido.Tipo);
        Assert.Equal(movimento.ValorCentavos, lido.ValorCentavos);
        Assert.Equal(movimento.Competencia, lido.Competencia);
        Assert.Equal(movimento.OcorridoEm, lido.OcorridoEm);
    }

    [Fact]
    public async Task ListarAsync_retorna_apenas_do_business_pedido()
    {
        var repo = CriarRepositorio();
        var quando = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        await repo.RegistrarAsync(MovimentoMrr.Novo(BusinessA, "as-a", "srv-a", 1000, quando));
        await repo.RegistrarAsync(MovimentoMrr.Novo(BusinessB, "as-b", "srv-b", 2000, quando));

        var lista = await repo.ListarAsync(BusinessA);

        var lido = Assert.Single(lista);
        Assert.Equal("as-a", lido.AssinaturaId);
    }

    [Fact]
    public async Task Varios_movimentos_do_mesmo_business_todos_persistem()
    {
        var repo = CriarRepositorio();
        var quando = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        await repo.RegistrarAsync(MovimentoMrr.Novo(BusinessA, "as-1", "srv-a", 1000, quando));
        await repo.RegistrarAsync(MovimentoMrr.Expansao(BusinessA, "as-1", "srv-a", 200, quando.AddMonths(1)));
        await repo.RegistrarAsync(MovimentoMrr.Churn(BusinessA, "as-1", "srv-a", 1200, quando.AddMonths(2)));

        var lista = await repo.ListarAsync(BusinessA);

        Assert.Equal(3, lista.Count);
    }
}
