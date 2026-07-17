using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// P0-5 (docs/financeiro/revisao-domain-fit-cnpj.md) — o CMV da peça consumida numa OS
/// (<see cref="CustoBaixadoPorOs"/>, publicado pelo Estoque) precisa chegar a
/// <c>fato_custo_diario</c> na corrente Servico, do mesmo jeito que <see cref="CustoBaixadoPorVenda"/>
/// já chega na corrente Comercio — sem isso a margem por OS (mão de obra + peça vendida − CMV da
/// peça) fica incomputável mesmo com <c>OsFaturada</c> ligada.
/// </summary>
public sealed class FatoCustoDiarioProjectionTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task CustoBaixadoPorOs_Baixa_AcumulaNaCorrenteServico()
    {
        var repositorio = new InMemoryFatoCustoDiarioRepository();
        var projecao = new FatoCustoDiarioProjection(repositorio);

        var ocorridoEm = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new CustoBaixadoPorOs("os-1", TenantId, "linha-1", 36_000, ocorridoEm), "os.custo:os-1:linha-1");

        var dia = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        var fato = await repositorio.ObterAsync(TenantId, dia, CorrenteDeReceita.Servico);
        Assert.Equal(36_000, fato!.CustoCentavos);

        // Não contamina a corrente Comercio.
        var comercio = await repositorio.ObterAsync(TenantId, dia, CorrenteDeReceita.Comercio);
        Assert.Null(comercio);
    }

    [Fact]
    public async Task CustoBaixadoPorOs_VariasLinhasDaMesmaOs_SomamNoMesmoDiaECorrente()
    {
        var repositorio = new InMemoryFatoCustoDiarioRepository();
        var projecao = new FatoCustoDiarioProjection(repositorio);

        var ocorridoEm = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new CustoBaixadoPorOs("os-1", TenantId, "linha-1", 36_000, ocorridoEm), "os.custo:os-1:linha-1");
        await AplicarAsync(projecao, new CustoBaixadoPorOs("os-1", TenantId, "linha-2", 12_500, ocorridoEm), "os.custo:os-1:linha-2");

        var dia = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        var fato = await repositorio.ObterAsync(TenantId, dia, CorrenteDeReceita.Servico);
        Assert.Equal(48_500, fato!.CustoCentavos);
    }

    /// <summary>Estorno chega com o sinal já invertido (negativo) — o fold só soma, nunca precisa
    /// saber se é baixa ou estorno.</summary>
    [Fact]
    public async Task CustoBaixadoPorOs_Estornado_SubtraiDoAcumuladoNoDiaDoEstorno()
    {
        var repositorio = new InMemoryFatoCustoDiarioRepository();
        var projecao = new FatoCustoDiarioProjection(repositorio);

        var dataBaixa = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new CustoBaixadoPorOs("os-2", TenantId, "linha-1", 36_000, dataBaixa), "os.custo:os-2:linha-1");

        var dataEstorno = new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(
            projecao, new CustoBaixadoPorOs("os-2", TenantId, "linha-1", -36_000, dataEstorno, Estornado: true),
            "os.custo.estorno:os-2:linha-1");

        var diaBaixa = BucketingTemporalDoTenant.DiaLocal(dataBaixa);
        var diaEstorno = BucketingTemporalDoTenant.DiaLocal(dataEstorno);

        Assert.Equal(36_000, (await repositorio.ObterAsync(TenantId, diaBaixa, CorrenteDeReceita.Servico))!.CustoCentavos);
        Assert.Equal(-36_000, (await repositorio.ObterAsync(TenantId, diaEstorno, CorrenteDeReceita.Servico))!.CustoCentavos);
    }

    private static Task AplicarAsync(FatoCustoDiarioProjection projecao, IIntegrationEvent evento, string chaveIdempotencia)
    {
        var entrada = new IntegrationEventLedgerEntry(
            Cursor: 1,
            Id: Guid.NewGuid().ToString("N"),
            Tipo: evento.GetType().Name,
            TenantId: evento.TenantId,
            PayloadJson: JsonSerializer.Serialize(evento, evento.GetType()),
            OcorridoEm: evento.OcorridoEm,
            ChaveIdempotencia: chaveIdempotencia,
            PersistidoEmUtc: DateTimeOffset.UtcNow);

        return projecao.AplicarAsync(entrada);
    }
}
