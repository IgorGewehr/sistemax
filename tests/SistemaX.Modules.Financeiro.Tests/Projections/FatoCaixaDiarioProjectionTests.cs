using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// P1-3 (docs/financeiro/revisao-domain-fit-cnpj.md) — <c>fato_caixa_diario</c> deixa de ser
/// unilateral: <c>ParcelaBaixada</c> (publicado por <c>BaixarParcelaUseCase</c>) alimenta tanto
/// SAÍDA (ContaAPagar liquidada — folha, compras, despesas, comissão) quanto ENTRADA (ContaAReceber
/// a prazo liquidada depois, ex.: cartão em D+N). Antes desta projeção, só entradas à vista
/// (VendaConcluida/PedidoPago) e a reversão de VendaEstornada alimentavam o fato — nenhuma saída de
/// pagamento de conta entrava, enviesando bandas/burn/runway para "sem queima de caixa".
/// </summary>
public sealed class FatoCaixaDiarioProjectionTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task ParcelaBaixada_Saida_AcumulaComoSaidaDoDia()
    {
        var repositorio = new InMemoryFatoCaixaDiarioRepository();
        var projecao = new FatoCaixaDiarioProjection(repositorio);

        var ocorridoEm = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new ParcelaBaixada("conta-1", "parcela-1", TenantId, EhAPagar: true, 50_000, ocorridoEm));

        var dia = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        var fato = await repositorio.ObterAsync(TenantId, dia);
        Assert.Equal(50_000, fato!.SaidasCentavos);
        Assert.Equal(0, fato.EntradasCentavos);
        Assert.Equal(-50_000, fato.SaldoDiaCentavos);
    }

    [Fact]
    public async Task ParcelaBaixada_Entrada_AcumulaComoEntradaDoDiaDaLiquidacao()
    {
        var repositorio = new InMemoryFatoCaixaDiarioRepository();
        var projecao = new FatoCaixaDiarioProjection(repositorio);

        var dataVenda = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.FromHours(-3));
        var dataLiquidacao = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(-3)); // D+30
        await AplicarAsync(projecao, new ParcelaBaixada("conta-2", "parcela-1", TenantId, EhAPagar: false, 30_000, dataLiquidacao));

        // Nada no dia da venda — só a liquidação move caixa.
        Assert.Null(await repositorio.ObterAsync(TenantId, BucketingTemporalDoTenant.DiaLocal(dataVenda)));

        var diaLiquidacao = BucketingTemporalDoTenant.DiaLocal(dataLiquidacao);
        var fato = await repositorio.ObterAsync(TenantId, diaLiquidacao);
        Assert.Equal(30_000, fato!.EntradasCentavos);
        Assert.Equal(30_000, fato.SaldoDiaCentavos);
    }

    /// <summary>Regressão do gap central de P1-3: com entradas E saídas de verdade, o saldo do dia
    /// pode ficar NEGATIVO (queima de caixa) — antes desta fatia, nenhuma saída entrava e o saldo
    /// nunca ficava abaixo de zero por causa de pagamento de conta.</summary>
    [Fact]
    public async Task DiaComEntradasESaidas_SaldoReflete_QueimaDeCaixaReal()
    {
        var repositorio = new InMemoryFatoCaixaDiarioRepository();
        var projecao = new FatoCaixaDiarioProjection(repositorio);

        var dia = new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new ParcelaBaixada("venda-x", "p1", TenantId, EhAPagar: false, 10_000, dia));
        await AplicarAsync(projecao, new ParcelaBaixada("folha-x", "p1", TenantId, EhAPagar: true, 45_000, dia.AddHours(2)), "parcela.baixada:folha-x:p1");

        var fato = await repositorio.ObterAsync(TenantId, BucketingTemporalDoTenant.DiaLocal(dia));
        Assert.Equal(10_000, fato!.EntradasCentavos);
        Assert.Equal(45_000, fato.SaidasCentavos);
        Assert.Equal(-35_000, fato.SaldoDiaCentavos);
    }

    /// <summary>MDR já resolvido no publicador (P1-6) — a projeção só soma o que chega, nunca
    /// recalcula taxa: uma entrada de R$1.000 brutos com MDR de 3,49% chega aqui como R$965,10
    /// líquidos (349 centavos de taxa já descontados por quem publicou o evento).</summary>
    [Fact]
    public async Task ParcelaBaixada_EntradaComMdrJaResolvidoNoPublicador_AcumulaOLiquido()
    {
        var repositorio = new InMemoryFatoCaixaDiarioRepository();
        var projecao = new FatoCaixaDiarioProjection(repositorio);

        var dataLiquidacao = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(-3));
        const long valorLiquidoJaDescontado = 96_510; // R$1.000,00 brutos - 3,49% de MDR
        await AplicarAsync(projecao, new ParcelaBaixada("conta-cartao", "p1", TenantId, EhAPagar: false, valorLiquidoJaDescontado, dataLiquidacao));

        var fato = await repositorio.ObterAsync(TenantId, BucketingTemporalDoTenant.DiaLocal(dataLiquidacao));
        Assert.Equal(valorLiquidoJaDescontado, fato!.EntradasCentavos);
    }

    private static Task AplicarAsync(FatoCaixaDiarioProjection projecao, IIntegrationEvent evento, string? chaveIdempotencia = null)
    {
        var entrada = new IntegrationEventLedgerEntry(
            Cursor: 1,
            Id: Guid.NewGuid().ToString("N"),
            Tipo: evento.GetType().Name,
            TenantId: evento.TenantId,
            PayloadJson: JsonSerializer.Serialize(evento, evento.GetType()),
            OcorridoEm: evento.OcorridoEm,
            ChaveIdempotencia: chaveIdempotencia ?? evento.ChaveIdempotencia,
            PersistidoEmUtc: DateTimeOffset.UtcNow);

        return projecao.AplicarAsync(entrada);
    }
}
