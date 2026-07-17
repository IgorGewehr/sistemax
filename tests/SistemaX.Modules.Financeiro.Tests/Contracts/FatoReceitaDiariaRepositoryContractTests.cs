using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFatoReceitaDiariaRepository"/> (F0 do plano de
/// inteligência do Financeiro — docs/financeiro/inteligencia-arquitetura.md/ADR-0005). A chave é
/// (tenant, dia, corrente) desde P0-1 (docs/financeiro/revisao-domain-fit-cnpj.md) — os casos
/// "isola por corrente" travam que a dimensão nova não vaza entre correntes do mesmo dia.</summary>
public abstract class FatoReceitaDiariaRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    private static readonly DateOnly Dia1 = new(2026, 7, 15);
    private static readonly DateOnly Dia2 = new(2026, 7, 16);

    protected abstract IFatoReceitaDiariaRepository CriarRepositorio();

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

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 10_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 5_000);

        var fato = await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio);
        Assert.NotNull(fato);
        Assert.Equal(15_000, fato!.ReceitaCentavos);
    }

    [Fact]
    public async Task Acumular_com_delta_negativo_reduz_a_receita_do_dia()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 10_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, -3_000); // ex.: VendaEstornada

        var fato = await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio);
        Assert.Equal(7_000, fato!.ReceitaCentavos);
    }

    [Fact]
    public async Task Acumular_isola_por_tenant()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 10_000);
        await repo.AcumularAsync(TenantB, Dia1, CorrenteDeReceita.Comercio, 99_000);

        Assert.Equal(10_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio))!.ReceitaCentavos);
        Assert.Equal(99_000, (await repo.ObterAsync(TenantB, Dia1, CorrenteDeReceita.Comercio))!.ReceitaCentavos);
    }

    /// <summary>P0-1 — a chave completa é (tenant, dia, corrente): Servico e Comercio no MESMO
    /// tenant/dia acumulam de forma independente, uma nunca vaza pra outra.</summary>
    [Fact]
    public async Task Acumular_isola_por_corrente_no_mesmo_tenant_e_dia()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Recorrente, 1_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Servico, 2_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 3_000);

        Assert.Equal(1_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Recorrente))!.ReceitaCentavos);
        Assert.Equal(2_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Servico))!.ReceitaCentavos);
        Assert.Equal(3_000, (await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio))!.ReceitaCentavos);
    }

    /// <summary>Somar TODAS as linhas de <c>ListarAsync</c> (independente da corrente) reproduz o
    /// total do dia — é a garantia que consumidores que só querem o agregado (Radar do Simples,
    /// Breakeven) continuam corretos sem precisar conhecer a dimensão nova.</summary>
    [Fact]
    public async Task Listar_soma_de_todas_as_correntes_do_dia_bate_com_o_total_acumulado()
    {
        var repo = CriarRepositorio();

        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Recorrente, 1_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Servico, 2_000);
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 3_000);

        var lista = await repo.ListarAsync(TenantA, Dia1, Dia1);

        Assert.Equal(3, lista.Count);
        Assert.Equal(6_000, lista.Sum(f => f.ReceitaCentavos));
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
        Assert.Equal(100, lista[0].ReceitaCentavos);
        Assert.Equal(Dia2, lista[1].Dia);
        Assert.Equal(200, lista[1].ReceitaCentavos);
    }

    [Fact]
    public async Task ZerarTudo_apaga_toda_a_fact_table()
    {
        var repo = CriarRepositorio();
        await repo.AcumularAsync(TenantA, Dia1, CorrenteDeReceita.Comercio, 10_000);
        await repo.AcumularAsync(TenantB, Dia2, CorrenteDeReceita.Comercio, 20_000);

        await repo.ZerarTudoAsync();

        Assert.Null(await repo.ObterAsync(TenantA, Dia1, CorrenteDeReceita.Comercio));
        Assert.Null(await repo.ObterAsync(TenantB, Dia2, CorrenteDeReceita.Comercio));
    }
}
