using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFatoCaixaDiarioRepository"/> (F0 do plano de
/// inteligência do Financeiro — docs/financeiro/inteligencia-arquitetura.md/ADR-0005).</summary>
public abstract class FatoCaixaDiarioRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    private static readonly DateOnly Dia1 = new(2026, 7, 15);
    private static readonly DateOnly Dia2 = new(2026, 7, 16);

    protected abstract IFatoCaixaDiarioRepository CriarRepositorio();

    [Fact]
    public async Task Obter_sem_nenhum_acumulo_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, Dia1));
    }

    [Fact]
    public async Task Entradas_e_saidas_acumulam_independentemente_e_saldo_e_derivado()
    {
        var repo = CriarRepositorio();

        await repo.AcumularEntradaAsync(TenantA, Dia1, 10_000);
        await repo.AcumularEntradaAsync(TenantA, Dia1, 5_000);
        await repo.AcumularSaidaAsync(TenantA, Dia1, 4_000);

        var fato = await repo.ObterAsync(TenantA, Dia1);
        Assert.NotNull(fato);
        Assert.Equal(15_000, fato!.EntradasCentavos);
        Assert.Equal(4_000, fato.SaidasCentavos);
        Assert.Equal(11_000, fato.SaldoDiaCentavos);
    }

    [Fact]
    public async Task Acumular_isola_por_tenant()
    {
        var repo = CriarRepositorio();

        await repo.AcumularEntradaAsync(TenantA, Dia1, 10_000);
        await repo.AcumularEntradaAsync(TenantB, Dia1, 99_000);

        Assert.Equal(10_000, (await repo.ObterAsync(TenantA, Dia1))!.EntradasCentavos);
        Assert.Equal(99_000, (await repo.ObterAsync(TenantB, Dia1))!.EntradasCentavos);
    }

    [Fact]
    public async Task Listar_retorna_apenas_o_periodo_pedido_ordenado_por_dia()
    {
        var repo = CriarRepositorio();
        var dia3 = Dia2.AddDays(1);

        await repo.AcumularEntradaAsync(TenantA, dia3, 300);
        await repo.AcumularEntradaAsync(TenantA, Dia1, 100);
        await repo.AcumularEntradaAsync(TenantA, Dia2, 200);

        var lista = await repo.ListarAsync(TenantA, Dia1, Dia2);

        Assert.Equal(2, lista.Count);
        Assert.Equal(Dia1, lista[0].Dia);
        Assert.Equal(Dia2, lista[1].Dia);
    }

    [Fact]
    public async Task ZerarTudo_apaga_toda_a_fact_table()
    {
        var repo = CriarRepositorio();
        await repo.AcumularEntradaAsync(TenantA, Dia1, 10_000);
        await repo.AcumularSaidaAsync(TenantB, Dia2, 20_000);

        await repo.ZerarTudoAsync();

        Assert.Null(await repo.ObterAsync(TenantA, Dia1));
        Assert.Null(await repo.ObterAsync(TenantB, Dia2));
    }
}
