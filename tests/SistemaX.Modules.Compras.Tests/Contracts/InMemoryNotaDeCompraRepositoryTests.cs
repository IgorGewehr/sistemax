using SistemaX.Modules.Compras.Domain.Notas;
using SistemaX.Modules.Compras.Infrastructure.InMemory;

namespace SistemaX.Modules.Compras.Tests.Contracts;

/// <summary>
/// Cobertura direta de <see cref="InMemoryNotaDeCompraRepository.ListarAsync"/> — read-model da
/// tela de Notas de Compra (achado de auditoria — ver <c>ComprasEndpointsModule</c>): sem ela o
/// front não tinha como enumerar notas, só resolvê-las já sabendo id/chave de acesso. Sem contract
/// test abstrato (irmão de <c>FornecedorRepositoryContractTests</c>) porque <c>INotaDeCompraRepository</c>
/// só tem adapter InMemory nesta rodada — <c>ComprasInfrastructureModule</c> ainda não portou este
/// port para SQLite (ver comentário lá).
/// </summary>
public sealed class InMemoryNotaDeCompraRepositoryTests
{
    private const string TenantA = "loja-a";
    private const string TenantB = "loja-b";

    [Fact]
    public async Task Listar_retorna_so_notas_do_tenant_mais_recente_primeiro()
    {
        var repo = new InMemoryNotaDeCompraRepository();
        var totais = ComprasTestBuilder.NotaEmConferencia().Totais;
        var agora = DateTimeOffset.UtcNow;

        var antiga = NotaDeCompra.Importar(
            TenantA, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1001", "1",
            agora, totais, [ComprasTestBuilder.Item(1, 10_000)]).Valor;
        await repo.SalvarAsync(antiga);

        var recente = NotaDeCompra.Importar(
            TenantA, ComprasTestBuilder.LojaId, OrigemNota.Manual, "1002", "1",
            agora.AddDays(1), totais, [ComprasTestBuilder.Item(1, 10_000)]).Valor;
        await repo.SalvarAsync(recente);

        var deOutroTenant = NotaDeCompra.Importar(
            TenantB, "loja-2", OrigemNota.Manual, "2001", "1",
            agora, totais, [ComprasTestBuilder.Item(1, 10_000)]).Valor;
        await repo.SalvarAsync(deOutroTenant);

        var lista = await repo.ListarAsync(TenantA);

        Assert.Equal(2, lista.Count);
        Assert.Equal(recente.Id, lista[0].Id);
        Assert.Equal(antiga.Id, lista[1].Id);
        Assert.All(lista, n => Assert.Equal(TenantA, n.TenantId));
    }

    [Fact]
    public async Task Listar_sem_notas_do_tenant_retorna_lista_vazia()
    {
        var repo = new InMemoryNotaDeCompraRepository();
        Assert.Empty(await repo.ListarAsync(TenantA));
    }
}
