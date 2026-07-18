using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// Matriz margem×popularidade — os 4 quadrantes clássicos (Estrela/VacaLeiteira/Enigma/Abacaxi),
/// sobre a MESMA fact table de <see cref="FoodCostServiceTests"/>. Cenário com 4 produtos, um em
/// cada quadrante, calculado à mão (ver comentário de cada produto).
/// </summary>
public sealed class EngenhariaDeCardapioServiceTests
{
    private const string BusinessId = "biz-engenharia-cardapio";
    private static readonly DateOnly Dia = new(2026, 8, 1);

    [Fact]
    public async Task ClassificarAsync_QuatroProdutos_ClassificaOsQuatroQuadrantes()
    {
        var repo = new InMemoryFatoMargemProdutoRepository();

        // margemMedia dos 4 = (75%+10%+90%+10%)/4 = 46,25%; participacaoMedia = 100%/4 = 25%.
        // Estrela: receita 4.000 (40% > 25%, popular), custo 1.000 -> margem 75% (> 46,25%, alta).
        await Seed(repo, "estrela", "venda-estrela", 4_000, 1_000);
        // VacaLeiteira: receita 4.000 (40%, popular), custo 3.600 -> margem 10% (baixa).
        await Seed(repo, "vaca-leiteira", "venda-vaca", 4_000, 3_600);
        // Enigma: receita 1.000 (10% < 25%, pouco popular), custo 100 -> margem 90% (alta).
        await Seed(repo, "enigma", "venda-enigma", 1_000, 100);
        // Abacaxi: receita 1.000 (10%, pouco popular), custo 900 -> margem 10% (baixa).
        await Seed(repo, "abacaxi", "venda-abacaxi", 1_000, 900);

        var servico = new EngenhariaDeCardapioService(repo);
        var linhas = await servico.ClassificarAsync(BusinessId, Dia, Dia);

        Assert.Equal(4, linhas.Count);
        Assert.Equal(QuadranteCardapio.Estrela, linhas.Single(l => l.ProdutoId == "estrela").Quadrante);
        Assert.Equal(QuadranteCardapio.VacaLeiteira, linhas.Single(l => l.ProdutoId == "vaca-leiteira").Quadrante);
        Assert.Equal(QuadranteCardapio.Enigma, linhas.Single(l => l.ProdutoId == "enigma").Quadrante);
        Assert.Equal(QuadranteCardapio.Abacaxi, linhas.Single(l => l.ProdutoId == "abacaxi").Quadrante);
    }

    [Fact]
    public async Task ClassificarAsync_SemVendaNoPeriodo_DevolveListaVazia_FailQuiet()
    {
        var servico = new EngenhariaDeCardapioService(new InMemoryFatoMargemProdutoRepository());

        var linhas = await servico.ClassificarAsync(BusinessId, Dia, Dia.AddDays(30));

        Assert.Empty(linhas);
    }

    private static async Task Seed(IFatoMargemProdutoRepository repo, string produtoId, string vendaId, long receitaCentavos, long custoCentavos)
    {
        await repo.RegistrarItensDeVendaAsync(BusinessId, vendaId, Dia, [new ItemMargemPendente(produtoId, receitaCentavos)]);
        await repo.AlocarCustoDeVendaAsync(BusinessId, vendaId, custoCentavos);
    }
}
