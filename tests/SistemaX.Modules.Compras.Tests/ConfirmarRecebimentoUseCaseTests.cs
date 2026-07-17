using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Infrastructure.InMemory;
using SistemaX.Modules.Compras.Tests.Fakes;

namespace SistemaX.Modules.Compras.Tests;

/// <summary>
/// <see cref="ConfirmarRecebimentoUseCase"/> — a ponte para "tudo alimenta o financeiro/estoque".
/// Verifica a ORDEM commit-depois-publica (R3 do projeto) e que os DOIS eventos de integração
/// (<c>CompraRecebida</c> para o Financeiro, <c>CompraItensRecebidos</c> companion para o Estoque)
/// saem lado a lado, com os valores corretos e a chave de idempotência derivada do fato.
/// </summary>
public class ConfirmarRecebimentoUseCaseTests
{
    private static async Task<(NotaDeCompra Nota, InMemoryNotaDeCompraRepository Notas, FakeIntegrationEventBus Bus)> MontarNotaEmConferenciaAsync()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9001", vProdCentavos: 10_000, vFreteCentavos: 500);
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        return (nota, notas, new FakeIntegrationEventBus());
    }

    [Fact]
    public async Task ExecutarAsync_Sucesso_SalvaAntesDePublicarEPublicaOsDoisEventosLadoALado()
    {
        var (nota, notas, bus) = await MontarNotaEmConferenciaAsync();
        var confirmar = new ConfirmarRecebimentoUseCase(notas, bus);

        var resultado = await confirmar.ExecutarAsync(nota.Id, "user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.Recebida, resultado.Valor.Status);

        var persistida = await notas.ObterPorIdAsync(nota.Id);
        Assert.Equal(StatusNotaDeCompra.Recebida, persistida!.Status); // commit local aconteceu

        Assert.Equal(2, bus.Publicados.Count);

        var compraRecebida = Assert.IsType<CompraRecebida>(bus.Publicados[0]);
        Assert.Equal(nota.Id, compraRecebida.CompraId);
        Assert.Equal(10_500, compraRecebida.TotalCentavos);
        Assert.Equal($"compra.recebida:{nota.Id}", compraRecebida.ChaveIdempotencia);

        var compraItensRecebidos = Assert.IsType<CompraItensRecebidos>(bus.Publicados[1]);
        Assert.Equal(nota.Id, compraItensRecebidos.CompraId);
        var item = Assert.Single(compraItensRecebidos.Itens);
        Assert.Equal("produto-1", item.ProdutoId);
        Assert.Equal(1000, item.QuantidadeMilesimos); // 1 UN, fator 1:1
        Assert.Equal($"compra.itens:{nota.Id}", compraItensRecebidos.ChaveIdempotencia);

        Assert.Empty(resultado.Valor.DomainEvents); // ClearDomainEvents() rodou depois de publicar
    }

    [Fact]
    public async Task ExecutarAsync_NotaInexistente_FalhaSemPublicarNada()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var bus = new FakeIntegrationEventBus();
        var confirmar = new ConfirmarRecebimentoUseCase(notas, bus);

        var resultado = await confirmar.ExecutarAsync("nota-que-nao-existe", "user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.nao_encontrada", resultado.Erro.Codigo);
        Assert.Empty(bus.Publicados);
    }

    [Fact]
    public async Task ExecutarAsync_ComItemSemMatchResolvido_FalhaSemPublicarNada()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var bus = new FakeIntegrationEventBus();
        var confirmar = new ConfirmarRecebimentoUseCase(notas, bus);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9002", produtoIdConhecido: null); // SemMatch
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        var resultado = await confirmar.ExecutarAsync(nota.Id, "user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.item.match_pendente", resultado.Erro.Codigo);
        Assert.Empty(bus.Publicados);

        var persistida = await notas.ObterPorIdAsync(nota.Id);
        Assert.Equal(StatusNotaDeCompra.EmConferencia, persistida!.Status); // não avançou
    }
}
