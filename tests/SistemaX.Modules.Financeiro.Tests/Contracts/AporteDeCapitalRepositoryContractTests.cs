using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IAporteDeCapitalRepository"/> — roda 2× (InMemory +
/// SQLite), mesmo molde de <c>AtivoDeCapitalRepositoryContractTests</c>, mais o delete físico
/// (DI5 do design de Imobilizado/ROI).</summary>
public abstract class AporteDeCapitalRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IAporteDeCapitalRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static AporteDeCapital NovoAporte(string businessId, decimal reais = 20_000, string descricao = "Capital de giro inicial")
        => AporteDeCapital.Criar(businessId, Money.DeReais(reais), new DateOnly(2026, 7, 1), descricao, Agora).Valor;

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_aporte()
    {
        var repo = CriarRepositorio();
        var aporte = NovoAporte(BusinessA);

        await repo.SalvarAsync(aporte);
        var lido = await repo.ObterPorIdAsync(BusinessA, aporte.Id);

        Assert.NotNull(lido);
        Assert.Equal(aporte.Id, lido!.Id);
        Assert.Equal(2_000_000, lido.Valor.Centavos);
        Assert.Equal(new DateOnly(2026, 7, 1), lido.Data);
        Assert.Equal("Capital de giro inicial", lido.Descricao);
    }

    [Fact]
    public async Task Obter_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var aporte = NovoAporte(BusinessA);
        await repo.SalvarAsync(aporte);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, aporte.Id));
    }

    [Fact]
    public async Task ListarAsync_retorna_so_do_tenant_ordenado_por_data()
    {
        var repo = CriarRepositorio();
        var depois = AporteDeCapital.Criar(BusinessA, Money.DeReais(5_000), new DateOnly(2026, 8, 1), "Reforço", Agora).Valor;
        var antes = AporteDeCapital.Criar(BusinessA, Money.DeReais(20_000), new DateOnly(2026, 7, 1), "Inicial", Agora).Valor;
        var deOutroTenant = NovoAporte(BusinessB);

        await repo.SalvarAsync(depois);
        await repo.SalvarAsync(antes);
        await repo.SalvarAsync(deOutroTenant);

        var lista = await repo.ListarAsync(BusinessA);
        Assert.Equal(2, lista.Count);
        Assert.Equal(antes.Id, lista[0].Id);
        Assert.Equal(depois.Id, lista[1].Id);
    }

    [Fact]
    public async Task ExcluirAsync_remove_o_aporte_e_e_idempotente()
    {
        var repo = CriarRepositorio();
        var aporte = NovoAporte(BusinessA);
        await repo.SalvarAsync(aporte);

        var removidoDaPrimeiraVez = await repo.ExcluirAsync(BusinessA, aporte.Id);
        var removidoDaSegundaVez = await repo.ExcluirAsync(BusinessA, aporte.Id);

        Assert.True(removidoDaPrimeiraVez);
        Assert.False(removidoDaSegundaVez);
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, aporte.Id));
    }

    [Fact]
    public async Task ExcluirAsync_de_outro_business_nao_remove()
    {
        var repo = CriarRepositorio();
        var aporte = NovoAporte(BusinessA);
        await repo.SalvarAsync(aporte);

        var removido = await repo.ExcluirAsync(BusinessB, aporte.Id);

        Assert.False(removido);
        Assert.NotNull(await repo.ObterPorIdAsync(BusinessA, aporte.Id));
    }
}
