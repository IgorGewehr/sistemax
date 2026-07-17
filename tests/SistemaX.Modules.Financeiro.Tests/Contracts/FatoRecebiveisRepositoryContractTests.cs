using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFatoRecebiveisRepository"/> — frente 3 da autonomia
/// do motor financeiro (líquido + MDR + lag D+N).</summary>
public abstract class FatoRecebiveisRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    private static readonly DateOnly Dia1 = new(2026, 7, 15);
    private static readonly DateOnly Dia2 = new(2026, 7, 16);

    protected abstract IFatoRecebiveisRepository CriarRepositorio();

    private static FatoRecebivel Linha(string tenantId, string origemChave, DateOnly vencimento, string? formaPagamento, long brutoCentavos)
        => new(tenantId, origemChave, vencimento, vencimento.AddDays(1), formaPagamento, 0.01m, brutoCentavos, brutoCentavos - 1, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Listar_sem_nenhuma_linha_retorna_vazio()
    {
        var repo = CriarRepositorio();
        Assert.Empty(await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia2));
    }

    [Fact]
    public async Task Adicionar_e_listar_preserva_todos_os_campos()
    {
        var repo = CriarRepositorio();
        await repo.AdicionarAsync(new FatoRecebivel(
            TenantA, "sale:venda-1", Dia1, Dia1.AddDays(30), "credito", 0.0349m, 10_000, 9_651, DateTimeOffset.UtcNow));

        var linha = Assert.Single(await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia1));
        Assert.Equal("sale:venda-1", linha.OrigemChave);
        Assert.Equal(Dia1, linha.Vencimento);
        Assert.Equal(Dia1.AddDays(30), linha.DataLiquidacaoPrevista);
        Assert.Equal("credito", linha.FormaPagamento);
        Assert.Equal(0.0349m, linha.TaxaPercentualAplicada);
        Assert.Equal(10_000, linha.ValorBrutoCentavos);
        Assert.Equal(9_651, linha.ValorLiquidoCentavos);
    }

    [Fact]
    public async Task Adicionar_e_permitir_forma_de_pagamento_nula_reversao_sem_forma_conhecida()
    {
        var repo = CriarRepositorio();
        await repo.AdicionarAsync(new FatoRecebivel(
            TenantA, "sale-reversal:venda-1", Dia1, Dia1, FormaPagamento: null, 0m, -10_000, -10_000, DateTimeOffset.UtcNow));

        var linha = Assert.Single(await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia1));
        Assert.Null(linha.FormaPagamento);
        Assert.Equal(-10_000, linha.ValorBrutoCentavos);
    }

    [Fact]
    public async Task Adicionar_e_APPEND_ONLY_duas_linhas_da_mesma_origem_nao_se_sobrescrevem()
    {
        var repo = CriarRepositorio();
        await repo.AdicionarAsync(Linha(TenantA, "sale:venda-1", Dia1, "credito", 10_000));
        await repo.AdicionarAsync(Linha(TenantA, "sale-reversal:venda-1", Dia2, null, -10_000));

        var lista = await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia2);
        Assert.Equal(2, lista.Count); // nunca edita a original — só acrescenta
    }

    [Fact]
    public async Task Listar_retorna_apenas_o_periodo_pedido_ordenado_por_vencimento()
    {
        var repo = CriarRepositorio();
        var dia3 = Dia2.AddDays(1);

        await repo.AdicionarAsync(Linha(TenantA, "c", dia3, "pix", 300));
        await repo.AdicionarAsync(Linha(TenantA, "a", Dia1, "pix", 100));
        await repo.AdicionarAsync(Linha(TenantA, "b", Dia2, "pix", 200));

        var lista = await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia2);

        Assert.Equal(2, lista.Count);
        Assert.Equal(Dia1, lista[0].Vencimento);
        Assert.Equal(Dia2, lista[1].Vencimento);
    }

    [Fact]
    public async Task Listar_isola_por_tenant()
    {
        var repo = CriarRepositorio();
        await repo.AdicionarAsync(Linha(TenantA, "a", Dia1, "pix", 100));
        await repo.AdicionarAsync(Linha(TenantB, "b", Dia1, "pix", 999));

        Assert.Single(await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia1));
        var linhaB = Assert.Single(await repo.ListarPorVencimentoAsync(TenantB, Dia1, Dia1));
        Assert.Equal(999, linhaB.ValorBrutoCentavos);
    }

    [Fact]
    public async Task ZerarTudo_apaga_toda_a_fact_table()
    {
        var repo = CriarRepositorio();
        await repo.AdicionarAsync(Linha(TenantA, "a", Dia1, "pix", 100));
        await repo.AdicionarAsync(Linha(TenantB, "b", Dia2, "pix", 200));

        await repo.ZerarTudoAsync();

        Assert.Empty(await repo.ListarPorVencimentoAsync(TenantA, Dia1, Dia1));
        Assert.Empty(await repo.ListarPorVencimentoAsync(TenantB, Dia2, Dia2));
    }
}
