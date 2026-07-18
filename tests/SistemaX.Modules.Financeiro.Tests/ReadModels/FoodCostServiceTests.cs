using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// LENTE VERTICAL ALIMENTAÇÃO — food cost % = CMV do prato ÷ receita, sobre <c>fato_margem_produto</c>
/// (a mesma fact table que <c>DreGerencialService</c>/<c>PontoDeEquilibrioService</c> já usam — zero
/// dado novo). O CMV aqui já é o "custo com ficha técnica" por construção (o Estoque expande o BOM
/// antes de baixar), então este teste não precisa simular BOM nenhum — só o par receita/custo que
/// <c>fato_margem_produto</c> exporia de qualquer prato composto.
/// </summary>
public sealed class FoodCostServiceTests
{
    private const string BusinessId = "biz-food-cost";
    private static readonly DateOnly Dia = new(2026, 8, 1);

    [Fact]
    public async Task CalcularAsync_PratoComFichaTecnicaJaExpandida_FoodCostPercentEhCustoSobreReceita()
    {
        var repo = new InMemoryFatoMargemProdutoRepository();

        // Prato "X-Burger": vendido por R$30, CMV de R$9 (ficha técnica já expandida no razão de
        // estoque ANTES de chegar em fato_margem_produto) -> food cost 30%.
        await repo.RegistrarItensDeVendaAsync(BusinessId, "venda-1", Dia, [new ItemMargemPendente("prato-xburger", 3_000)]);
        await repo.AlocarCustoDeVendaAsync(BusinessId, "venda-1", 900);

        var servico = new FoodCostService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, Dia, Dia);

        var linha = Assert.Single(linhas);
        Assert.Equal("prato-xburger", linha.ProdutoId);
        Assert.Equal(3_000, linha.ReceitaCentavos);
        Assert.Equal(900, linha.CustoCentavos);
        Assert.Equal(0.30, linha.FoodCostPercent, precision: 10);
    }

    [Fact]
    public async Task CalcularAsync_DoisPratosNoMesmoPeriodo_OrdenaPeloMaiorFoodCostPercent()
    {
        var repo = new InMemoryFatoMargemProdutoRepository();

        // Prato caro de operar: food cost 40%.
        await repo.RegistrarItensDeVendaAsync(BusinessId, "venda-caro", Dia, [new ItemMargemPendente("prato-caro", 10_000)]);
        await repo.AlocarCustoDeVendaAsync(BusinessId, "venda-caro", 4_000);

        // Prato enxuto: food cost 15%.
        await repo.RegistrarItensDeVendaAsync(BusinessId, "venda-enxuto", Dia, [new ItemMargemPendente("prato-enxuto", 10_000)]);
        await repo.AlocarCustoDeVendaAsync(BusinessId, "venda-enxuto", 1_500);

        var servico = new FoodCostService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, Dia, Dia);

        Assert.Equal(2, linhas.Count);
        Assert.Equal("prato-caro", linhas[0].ProdutoId);
        Assert.Equal("prato-enxuto", linhas[1].ProdutoId);
    }

    [Fact]
    public async Task CalcularAsync_SemVendaNoPeriodo_DevolveListaVazia_FailQuiet()
    {
        var servico = new FoodCostService(new InMemoryFatoMargemProdutoRepository());

        var linhas = await servico.CalcularAsync(BusinessId, Dia, Dia.AddDays(30));

        Assert.Empty(linhas);
    }
}
