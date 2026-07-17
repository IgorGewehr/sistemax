using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IFatoMargemProdutoRepository"/> — mesmo molde de
/// <see cref="FatoCustoDiarioRepositoryContractTests"/> (F1 do plano de inteligência do Financeiro
/// — docs/financeiro/inteligencia-arquitetura.md/ADR-0005), mais os cenários específicos do
/// estado de transição pendente→custo alocado.</summary>
public abstract class FatoMargemProdutoRepositoryContractTests
{
    protected const string TenantA = "tenant-a";
    protected const string TenantB = "tenant-b";
    private static readonly DateOnly Dia1 = new(2026, 7, 15);
    private static readonly DateOnly Dia2 = new(2026, 7, 16);

    protected abstract IFatoMargemProdutoRepository CriarRepositorio();

    [Fact]
    public async Task Obter_sem_nenhum_acumulo_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterAsync(TenantA, "produto-1", Dia1));
    }

    [Fact]
    public async Task Registrar_itens_de_venda_acumula_receita_imediatamente_por_produto()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 1_000), new ItemMargemPendente("produto-2", 500)]);

        Assert.Equal(1_000, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.ReceitaCentavos);
        Assert.Equal(500, (await repo.ObterAsync(TenantA, "produto-2", Dia1))!.ReceitaCentavos);
        Assert.Equal(0, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.CustoCentavos);
    }

    [Fact]
    public async Task Alocar_custo_rateia_proporcional_a_receita_dos_itens_pendentes()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1,
            [new ItemMargemPendente("produto-1", 750), new ItemMargemPendente("produto-2", 250)]);

        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 400);

        // 750/1000=75% -> 300; 250/1000=25% -> 100
        Assert.Equal(300, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.CustoCentavos);
        Assert.Equal(100, (await repo.ObterAsync(TenantA, "produto-2", Dia1))!.CustoCentavos);
    }

    [Fact]
    public async Task Margem_de_contribuicao_e_receita_menos_custo()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 1_000)]);
        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 400);

        var fato = await repo.ObterAsync(TenantA, "produto-1", Dia1);
        Assert.Equal(600, fato!.MargemContribuicaoCentavos);
    }

    [Fact]
    public async Task Alocar_custo_sem_itens_pendentes_e_no_op_silencioso()
    {
        var repo = CriarRepositorio();
        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-inexistente", 500);
        // não lança, e não cria fato nenhum
        var lista = await repo.ListarAsync(TenantA, Dia1, Dia2);
        Assert.Empty(lista);
    }

    [Fact]
    public async Task Alocar_custo_consome_o_estado_pendente_nao_permitindo_alocacao_dupla()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 1_000)]);
        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 400);
        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 999); // replay/duplicata — não deveria fazer nada

        Assert.Equal(400, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.CustoCentavos);
    }

    [Fact]
    public async Task Isola_por_tenant()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 1_000)]);
        await repo.RegistrarItensDeVendaAsync(TenantB, "venda-2", Dia1, [new ItemMargemPendente("produto-1", 2_000)]);

        Assert.Equal(1_000, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.ReceitaCentavos);
        Assert.Equal(2_000, (await repo.ObterAsync(TenantB, "produto-1", Dia1))!.ReceitaCentavos);
    }

    [Fact]
    public async Task Listar_retorna_apenas_o_periodo_pedido_ordenado_por_dia()
    {
        var repo = CriarRepositorio();
        var dia3 = Dia2.AddDays(1);

        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-3", dia3, [new ItemMargemPendente("produto-1", 300)]);
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 100)]);
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-2", Dia2, [new ItemMargemPendente("produto-1", 200)]);

        var lista = await repo.ListarAsync(TenantA, Dia1, Dia2);

        Assert.Equal(2, lista.Count);
        Assert.Equal(Dia1, lista[0].Dia);
        Assert.Equal(Dia2, lista[1].Dia);
    }

    [Fact]
    public async Task Listar_por_produto_filtra_so_o_produto_pedido()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 100), new ItemMargemPendente("produto-2", 200)]);

        var lista = await repo.ListarPorProdutoAsync(TenantA, "produto-2", Dia1, Dia2);

        Assert.Single(lista);
        Assert.Equal("produto-2", lista[0].ProdutoId);
        Assert.Equal(200, lista[0].ReceitaCentavos);
    }

    [Fact]
    public async Task ZerarTudo_apaga_toda_a_fact_table_e_o_estado_pendente()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1, [new ItemMargemPendente("produto-1", 1_000)]);

        await repo.ZerarTudoAsync();

        Assert.Null(await repo.ObterAsync(TenantA, "produto-1", Dia1));
        // depois de zerar, alocar custo de uma venda "antiga" não deveria ressuscitar nada
        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 500);
        Assert.Null(await repo.ObterAsync(TenantA, "produto-1", Dia1));
    }

    [Fact]
    public async Task Mesmo_produto_em_duas_linhas_da_mesma_venda_soma_a_receita_antes_do_rateio()
    {
        var repo = CriarRepositorio();
        await repo.RegistrarItensDeVendaAsync(TenantA, "venda-1", Dia1,
            [new ItemMargemPendente("produto-1", 600), new ItemMargemPendente("produto-1", 400)]);

        Assert.Equal(1_000, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.ReceitaCentavos);

        await repo.AlocarCustoDeVendaAsync(TenantA, "venda-1", 200);
        Assert.Equal(200, (await repo.ObterAsync(TenantA, "produto-1", Dia1))!.CustoCentavos);
    }
}
