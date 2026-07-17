using SistemaX.Modules.Compras.Domain.Vinculos;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests;

public class VinculoProdutoFornecedorTests
{
    [Fact]
    public void Criar_ComFatorZeroOuNegativo_Falha()
    {
        var resultado = VinculoProdutoFornecedor.Criar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.FornecedorId, "CPROD-1", "produto-1", fatorConversaoMilesimos: 0, "nota-1");

        Assert.True(resultado.Falha);
        Assert.Equal("compras.vinculo.fator_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void AtualizarMatch_PreservaId_NuncaDuplicaRegistro()
    {
        var vinculo = VinculoProdutoFornecedor.Criar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.FornecedorId, "CPROD-1", "produto-1", 1000, "nota-1").Valor;
        var idOriginal = vinculo.Id;

        var resultado = vinculo.AtualizarMatch("produto-2", 12_000, "nota-2", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(idOriginal, vinculo.Id);
        Assert.Equal("produto-2", vinculo.ProdutoId);
        Assert.Equal(12_000, vinculo.FatorConversaoMilesimos);
        Assert.Equal("nota-2", vinculo.AprendidoDaNotaId);
    }

    [Fact]
    public async Task Repositorio_SalvarDuasVezesMesmaChave_SobrescreveEmVezDeDuplicar()
    {
        var repositorio = new InMemoryVinculoProdutoFornecedorRepository();
        var v1 = VinculoProdutoFornecedor.Criar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.FornecedorId, "CPROD-1", "produto-1", 1000, "nota-1").Valor;
        await repositorio.SalvarAsync(v1);

        var v2 = VinculoProdutoFornecedor.Criar(
            ComprasTestBuilder.TenantId, ComprasTestBuilder.FornecedorId, "CPROD-1", "produto-2", 2000, "nota-2").Valor;
        await repositorio.SalvarAsync(v2);

        var encontrado = await repositorio.ObterAsync(ComprasTestBuilder.TenantId, ComprasTestBuilder.FornecedorId, "CPROD-1");
        Assert.Equal("produto-2", encontrado!.ProdutoId);
    }
}
