using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests;

public class IgnorarItemEDescartarNotaUseCaseTests
{
    [Fact]
    public async Task IgnorarItemDaNotaUseCase_ItemExistente_MarcaComoIgnorado()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var ignorar = new IgnorarItemDaNotaUseCase(notas);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9201");
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        var resultado = await ignorar.ExecutarAsync(nota.Id, 1, "Amostra grátis");

        Assert.True(resultado.Sucesso);
        var persistida = await notas.ObterPorIdAsync(nota.Id);
        Assert.Equal(MatchState.Ignorado, persistida!.Itens[0].MatchState);
    }

    [Fact]
    public async Task IgnorarItemDaNotaUseCase_ItemInexistente_Falha()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var ignorar = new IgnorarItemDaNotaUseCase(notas);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9202");
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        var resultado = await ignorar.ExecutarAsync(nota.Id, 99, "não existe");

        Assert.True(resultado.Falha);
        Assert.Equal("compras.item.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task DescartarNotaUseCase_NotaEmConferencia_TransicionaParaDescartada()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var descartar = new DescartarNotaUseCase(notas);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9203");
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        var resultado = await descartar.ExecutarAsync(nota.Id, "Nota emitida para outro CNPJ");

        Assert.True(resultado.Sucesso);
        var persistida = await notas.ObterPorIdAsync(nota.Id);
        Assert.Equal(StatusNotaDeCompra.Descartada, persistida!.Status);
    }

    [Fact]
    public async Task DescartarNotaUseCase_NotaInexistente_Falha()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var descartar = new DescartarNotaUseCase(notas);

        var resultado = await descartar.ExecutarAsync("nota-que-nao-existe", "motivo");

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.nao_encontrada", resultado.Erro.Codigo);
    }
}
