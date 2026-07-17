using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Application.CasosDeUso;
using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Infrastructure.InMemory;
using SistemaX.Modules.Compras.Tests.Fakes;

namespace SistemaX.Modules.Compras.Tests;

public class EstornarRecebimentoUseCaseTests
{
    [Fact]
    public async Task ExecutarAsync_NotaRecebida_VoltaParaEmConferenciaEPublicaCompraEstornada()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var bus = new FakeIntegrationEventBus();
        var confirmar = new ConfirmarRecebimentoUseCase(notas, bus);
        var estornar = new EstornarRecebimentoUseCase(notas, bus);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9101", vProdCentavos: 10_000);
        var nota = (await registrar.ExecutarAsync(input)).Valor;
        await confirmar.ExecutarAsync(nota.Id, "user-1", "Operador", DateTimeOffset.UtcNow);

        var resultado = await estornar.ExecutarAsync(nota.Id, "user-2", "Supervisor", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusNotaDeCompra.EmConferencia, resultado.Valor.Status);

        Assert.Equal(3, bus.Publicados.Count); // CompraRecebida + CompraItensRecebidos + CompraEstornada
        var compraEstornada = Assert.IsType<CompraEstornada>(bus.Publicados[^1]);
        Assert.Equal(nota.Id, compraEstornada.CompraId);
        Assert.Equal(10_000, compraEstornada.TotalCentavos);
        Assert.Single(compraEstornada.Itens);
        Assert.Equal($"compra.estornada:{nota.Id}", compraEstornada.ChaveIdempotencia);
    }

    [Fact]
    public async Task ExecutarAsync_NotaAindaNaoRecebida_FalhaSemPublicarNada()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var vinculos = new InMemoryVinculoProdutoFornecedorRepository();
        var registrar = new RegistrarEntradaDeNotaUseCase(notas, vinculos);
        var bus = new FakeIntegrationEventBus();
        var estornar = new EstornarRecebimentoUseCase(notas, bus);

        var input = ComprasTestBuilder.EntradaComItemConhecido(numero: "9102");
        var nota = (await registrar.ExecutarAsync(input)).Valor;

        var resultado = await estornar.ExecutarAsync(nota.Id, "user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fsm.transicao_invalida", resultado.Erro.Codigo);
        Assert.Empty(bus.Publicados);
    }

    [Fact]
    public async Task ExecutarAsync_NotaInexistente_Falha()
    {
        var notas = new InMemoryNotaDeCompraRepository();
        var bus = new FakeIntegrationEventBus();
        var estornar = new EstornarRecebimentoUseCase(notas, bus);

        var resultado = await estornar.ExecutarAsync("nota-que-nao-existe", "user-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("compras.nota.nao_encontrada", resultado.Erro.Codigo);
    }
}
