using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFatoCustoDiarioRepository"/> — mesmo molde de
/// <see cref="FatoReceitaDiariaRepositoryContractTests"/> (F0 do plano de inteligência do
/// Financeiro — docs/financeiro/inteligencia-arquitetura.md/ADR-0005), com a chave
/// (tenant, dia, corrente) desde P0-1 (docs/financeiro/revisao-domain-fit-cnpj.md).</summary>
public abstract class FatoCustoDiarioRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    private static readonly DateOnly Dia1 = new(2026, 7, 15);
    private static readonly DateOnly Dia2 = new(2026, 7, 16);

    protected abstract IFatoCustoDiarioRepository CriarRepositorio();

    [Fact]
    public async Task Obter_sem_nenhum_acumulo_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio));
    }

    [Fact]
    public async Task Acumular_soma_sobre_o_valor_ja_existente_do_dia()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 4_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 1_500);

        var fato = await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio);
        Assert.NotNull(fato);
        Assert.Equal(5_500, fato!.CustoCentavos);
    }

    [Fact]
    public async Task Acumular_isola_por_tenant()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 4_000);
        await repo.AcumularAsync(TenantB, Dia1, CorrenteDeReceita.Comercio, 9_000);

        Assert.Equal(4_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio))!.CustoCentavos);
        Assert.Equal(9_000, (await repo.ObterAsync(TenantB, Dia1, CorrenteDeReceita.Comercio))!.CustoCentavos);
    }

    /// <summary>P0-1 — CMV de Servico (ex.: peça consumida em OS) e CMV de Comercio (venda de
    /// balcão) no MESMO tenant/dia acumulam de forma independente.</summary>
    [Fact]
    public async Task Acumular_isola_por_corrente_no_mesmo_tenant_e_dia()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Servico, 700);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 4_000);

        Assert.Equal(700, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Servico))!.CustoCentavos);
        Assert.Equal(4_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio))!.CustoCentavos);
    }

    [Fact]
    public async Task Listar_soma_de_todas_as_correntes_do_dia_bate_com_o_total_acumulado()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Servico, 700);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 4_000);

        var lista = await repo.ListarAsync(TenantA, Dia1, Dia1);

        Assert.Equal(2, lista.Count);
        Assert.Equal(4_700, lista.Sum(f => f.CustoCentavos));
    }

    [Fact]
    public async Task Listar_retorna_apenas_o_periodo_pedido_ordenado_por_dia()
    {
        var repo = CriarRepositorio();
        var dia3 = Dia2.AddDays(1);

        await repo.AcumularAsync(TenantA, dia3, CorrenteDeReceita.Comercio, 300);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 100);
        await repo.AcumularAsync(TenantA, Dia2, CorrenteDeReceita.Comercio, 200);

        var lista = await repo.ListarAsync(TenantA, Dia1, Dia2);

        Assert.Equal(2, lista.Count);
        Assert.Equal(Dia1, lista[0].Dia);
        Assert.Equal(100, lista[0].CustoCentavos);
        Assert.Equal(Dia2, lista[1].Dia);
        Assert.Equal(200, lista[1].CustoCentavos);
    }

    [Fact]
    public async Task ZerarTudo_apaga_toda_a_fact_table()
    {
        var repo = CriarRepositorio();
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 4_000);
        await repo.AcumularAsync(TenantB, Dia2, CorrenteDeReceita.Comercio, 9_000);

        await repo.ZerarTudoAsync();

        Assert.Null(await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio));
        Assert.Null(await repo.ObterAsync(TenantB, Dia2, CorrenteDeReceita.Comercio));
    }
}
