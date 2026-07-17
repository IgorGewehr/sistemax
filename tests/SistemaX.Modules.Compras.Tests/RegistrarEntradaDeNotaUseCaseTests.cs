using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests;

/// <summary>
/// O motor de match em cascata (plano §5) — a superação central do módulo: 1ª nota de um
/// fornecedor exige conferência manual, 2ª nota em diante é ZERO-TOUCH via
/// <c>VinculoProdutoFornecedor</c> aprendido.
/// </summary>
public class RegistrarEntradaDeNotaUseCaseTests
{
    [Fact]
    public async Task ExecutarAsync_SemVinculoENemProdutoConhecido_ItemFicaSemMatch()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "1", produtoIdConhecido: null);

        var resultado = await registrar.ExecutarAsync(input);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.EmConferencia, resultado.Valor.Status);
        Assert.Equal(MatchState.SemMatch, resultado.Valor.Itens[0].MatchState);
    }

    [Fact]
    public async Task ExecutarAsync_ComProdutoConhecido_ItemFicaManual()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "2");

        var resultado = await registrar.ExecutarAsync(input);

        Assert.Equal(MatchState.Manual, resultado.Valor.Itens[0].MatchState);
        Assert.Equal("produto-1", resultado.Valor.Itens[0].ProdutoId);
    }

    [Fact]
    public async Task ExecutarAsync_SegundaNotaMesmoFornecedorEMesmoCProd_ResolveAutoZeroTouch()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var resolverMatch = new ResolverMatchDeItemUseCase(notas, vinculos);

        // 1ª nota: sem match, humano resolve manualmente (aprende o vínculo).
        var primeiraEntrada = ComprasTestBuilder.EntradaComItemConhecido(numero: "3", produtoIdConhecido: null);
        var primeiraNota = (await registrar.ExecutarAsync(primeiraEntrada)).Valor;
        await resolverMatch.ExecutarAsync(primeiraNota.Id, 1, "produto-1", 1000);

        // 2ª nota do MESMO fornecedor, MESMO cProd — deve cair direto em Auto.
        var segundaEntrada = ComprasTestBuilder.EntradaComItemConhecido(numero: "4", produtoIdConhecido: null);
        var segundaNota = (await registrar.ExecutarAsync(segundaEntrada)).Valor;

        Assert.Equal(MatchState.Auto, segundaNota.Itens[0].MatchState);
        Assert.Equal("produto-1", segundaNota.Itens[0].ProdutoId);
    }

    [Fact]
    public async Task ExecutarAsync_ReimportarMesmaChaveDeAcesso_RetornaNotaExistenteSemDuplicar()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "5");

        var primeira = await registrar.ExecutarAsync(input);
        var segunda = await registrar.ExecutarAsync(input); // mesmo XML reimportado

        Assert.Equal(primeira.Valor.Id, segunda.Valor.Id);
    }

    [Fact]
    public async Task ExecutarAsync_ChaveDeAcessoInvalida_Falha()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "6") with { ChaveDeAcessoBruta = "123" };

        var resultado = await registrar.ExecutarAsync(input);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.chave_acesso.invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ResolverMatchDeItemUseCase_NotaInexistente_Falha()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var resolverMatch = new ResolverMatchDeItemUseCase(notas, vinculos);

        var resultado = await resolverMatch.ExecutarAsync("nota-que-nao-existe", 1, "produto-1", 1000);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.nao_encontrada", resultado.Erro.Codigo);
    }
}
