using SistemaX.Verticals.Assistencia.Infrastructure.InMemory;

namespace SistemaX.Verticals.Assistencia.Tests;

/// <summary>
/// Cobertura direta de <see cref="InMemoryOrdemDeServicoRepository.ListarAsync"/> — read-model da
/// fila de OS (achado de auditoria — ver <c>AssistenciaEndpointsModule</c>): sem ela o front não
/// tinha como enumerar/filtrar a fila, só resolver uma OS já sabendo o id.
/// </summary>
public sealed class InMemoryOrdemDeServicoRepositoryTests
{
    private const string TenantB = "tenant-2";

    [Fact]
    public async Task Listar_retorna_so_ordens_do_tenant_mais_recente_primeiro()
    {
        var repo = new InMemoryOrdemDeServicoRepository();

        var antiga = OrdemDeServico.Abrir(
            OrdemDeServicoTestBuilder.TenantId, "OS-0001", OrdemDeServicoTestBuilder.Cliente(), OrdemDeServicoTestBuilder.Equipamento(),
            "defeito 1", OrdemDeServicoTestBuilder.Abertura);
        await repo.SalvarAsync(antiga);

        var recente = OrdemDeServico.Abrir(
            OrdemDeServicoTestBuilder.TenantId, "OS-0002", OrdemDeServicoTestBuilder.Cliente(), OrdemDeServicoTestBuilder.Equipamento(),
            "defeito 2", OrdemDeServicoTestBuilder.Abertura.AddDays(1));
        await repo.SalvarAsync(recente);

        var deOutroTenant = OrdemDeServico.Abrir(
            TenantB, "OS-9001", OrdemDeServicoTestBuilder.Cliente(), OrdemDeServicoTestBuilder.Equipamento(),
            "defeito outro tenant", OrdemDeServicoTestBuilder.Abertura);
        await repo.SalvarAsync(deOutroTenant);

        var lista = await repo.ListarAsync(OrdemDeServicoTestBuilder.TenantId);

        Assert.Equal(2, lista.Count);
        Assert.Equal(recente.Id, lista[0].Id);
        Assert.Equal(antiga.Id, lista[1].Id);
        Assert.All(lista, os => Assert.Equal(OrdemDeServicoTestBuilder.TenantId, os.TenantId));
    }

    [Fact]
    public async Task Listar_com_filtro_de_status_so_retorna_o_status_pedido()
    {
        var repo = new InMemoryOrdemDeServicoRepository();
        var aberta = OrdemDeServicoTestBuilder.AbrirNova("OS-0001");
        var emDiagnostico = OrdemDeServicoTestBuilder.AteEmDiagnostico();

        await repo.SalvarAsync(aberta);
        await repo.SalvarAsync(emDiagnostico);

        var lista = await repo.ListarAsync(OrdemDeServicoTestBuilder.TenantId, StatusOrdemServico.EmDiagnostico);

        Assert.Single(lista);
        Assert.Equal(emDiagnostico.Id, lista[0].Id);
    }

    [Fact]
    public async Task Listar_sem_ordens_do_tenant_retorna_lista_vazia()
    {
        var repo = new InMemoryOrdemDeServicoRepository();
        Assert.Empty(await repo.ListarAsync(OrdemDeServicoTestBuilder.TenantId));
    }
}
