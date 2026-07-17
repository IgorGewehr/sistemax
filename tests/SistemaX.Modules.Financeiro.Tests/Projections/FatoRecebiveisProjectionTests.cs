using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Analitico;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.Projections;

/// <summary>
/// Prova a frente 3 da autonomia do motor financeiro: <c>fato_recebiveis</c> reflete o valor
/// LÍQUIDO (MDR já descontado) e a DATA em que o dinheiro efetivamente cai (vencimento + lag D+N),
/// nunca só o bruto/emissão — os dois casos que o board pedia (cartão D+30 vs. à vista D+0) mais o
/// caminho de reversão.
///
/// RECONCILIADO com o domínio Bancário: MDR/lag agora vêm de <see cref="IFormaDePagamentoRepository"/>
/// (o LAR ÚNICO), não mais de uma config estática — <see cref="FormasDePagamentoDeMercado"/> seeda um
/// repositório in-memory com as MESMAS taxas/lags que a antiga <c>ConfiguracaoDeRecebiveisOptions.PadraoDeMercado</c>
/// hardcodava, provando que a reconciliação preserva os números originais bit a bit.
/// </summary>
public sealed class FatoRecebiveisProjectionTests
{
    private const string TenantId = "tenant-1";

    [Fact]
    public async Task VendaConcluida_AVista_LiquidoIgualAoBrutoEDataDeLiquidacaoNoMesmoDia()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEm = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new VendaConcluida("venda-a-vista", TenantId, 10_000, "pix", ocorridoEm), "venda.concluida:venda-a-vista");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        var diaEsperado = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        Assert.Equal(diaEsperado, linha.Vencimento);
        Assert.Equal(diaEsperado, linha.DataLiquidacaoPrevista); // D+0 — cai no mesmo dia
        Assert.Equal(10_000, linha.ValorBrutoCentavos);
        Assert.Equal(10_000, linha.ValorLiquidoCentavos); // pix sem MDR — líquido = bruto
        Assert.Equal(0m, linha.TaxaPercentualAplicada);
    }

    [Fact]
    public async Task VendaConcluida_Credito_DescontaMdrEDeslocaLiquidacaoParaDMais30()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEm = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new VendaConcluida("venda-credito", TenantId, 10_000, "credito", ocorridoEm), "venda.concluida:venda-credito");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        var vencimento = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        Assert.Equal(vencimento, linha.Vencimento);
        Assert.Equal(vencimento.AddDays(30), linha.DataLiquidacaoPrevista); // D+30 do crédito
        Assert.Equal(10_000, linha.ValorBrutoCentavos);
        Assert.Equal(0.0349m, linha.TaxaPercentualAplicada);
        Assert.Equal(9_651, linha.ValorLiquidoCentavos); // 10000 - round(10000*0.0349) = 10000 - 349
    }

    [Fact]
    public async Task PedidoPago_Debito_AplicaTaxaELagConfiguradosParaDebito()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEm = new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new PedidoPago("pedido-1", TenantId, 5_000, "debito", ocorridoEm), "pedido.pago:pedido-1");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));

        var vencimento = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        Assert.Equal(vencimento.AddDays(1), linha.DataLiquidacaoPrevista); // D+1 do débito
        Assert.Equal(5_000 - 70, linha.ValorLiquidoCentavos); // round(5000*0.0139) = 70 (arredondamento bancário)
    }

    [Fact]
    public async Task VendaEstornada_LancaLinhaNegativaNoDiaDoEstornoSemMdr()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEmVenda = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new VendaConcluida("venda-estorno", TenantId, 10_000, "credito", ocorridoEmVenda), "venda.concluida:venda-estorno");

        var ocorridoEmEstorno = new DateTimeOffset(2026, 3, 12, 9, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new VendaEstornada("venda-estorno", TenantId, 10_000, ocorridoEmEstorno), "venda.estornada:venda-estorno");

        var linhas = await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(2, linhas.Count); // original + reversão — nunca edita/apaga a original

        var reversao = linhas.Single(l => l.ValorBrutoCentavos < 0);
        Assert.Equal(BucketingTemporalDoTenant.DiaLocal(ocorridoEmEstorno), reversao.Vencimento);
        Assert.Equal(-10_000, reversao.ValorBrutoCentavos);
        Assert.Equal(-10_000, reversao.ValorLiquidoCentavos); // sem forma de pagamento conhecida — sem MDR
        Assert.Null(reversao.FormaPagamento);

        var somaLiquida = linhas.Sum(l => l.ValorLiquidoCentavos);
        Assert.Equal(9_651 - 10_000, somaLiquida); // líquido original (crédito) revertido pelo bruto integral
    }

    /// <summary>P1-7 (docs/financeiro/revisao-domain-fit-cnpj.md) — OS com forma de pagamento
    /// informada ganha o MESMO tratamento de MDR/lag de venda/pedido.</summary>
    [Fact]
    public async Task OsFaturada_ComFormaDePagamento_AplicaTaxaELagDaForma()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEm = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao,
            new OsFaturada("os-1", TenantId, 12_000, 3_000, ocorridoEm, FormaPagamento: "debito", ClienteId: "cliente-1", NumeroOs: "OS-0001"),
            "os.faturada:os-1");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
        var vencimento = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        Assert.Equal(vencimento.AddDays(1), linha.DataLiquidacaoPrevista); // D+1 do débito
        Assert.Equal(15_000, linha.ValorBrutoCentavos); // 12000 (serviço) + 3000 (peças)
        Assert.Equal(15_000 - 208, linha.ValorLiquidoCentavos); // 15000*0.0139 = 208,5 -> arredondamento bancário pro par (208)
    }

    /// <summary>Taxa de diagnóstico de <c>DevolverSemReparo</c> (única OS sem forma de pagamento) —
    /// mesmo fallback conservador (0%, D+0) de qualquer forma desconhecida/ausente.</summary>
    [Fact]
    public async Task OsFaturada_SemFormaDePagamento_CaiNoFallbackConservadorSemMdr()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        var ocorridoEm = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new OsFaturada("os-2", TenantId, 5_000, 0, ocorridoEm), "os.faturada:os-2");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
        Assert.Equal(0m, linha.TaxaPercentualAplicada);
        Assert.Equal(5_000, linha.ValorLiquidoCentavos);
        Assert.Equal(linha.Vencimento, linha.DataLiquidacaoPrevista);
    }

    [Fact]
    public async Task ResetarAsync_ZeraTudo()
    {
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, await FormasDePagamentoDeMercado());

        await AplicarAsync(projecao, new VendaConcluida("venda-x", TenantId, 1_000, "dinheiro", DateTimeOffset.UtcNow), "venda.concluida:venda-x");
        await projecao.ResetarAsync();

        Assert.Empty(await repositorio.ListarPorVencimentoAsync(TenantId, DateOnly.MinValue, DateOnly.MaxValue));
    }

    [Fact]
    public async Task FormaDePagamento_NaoCadastrada_CaiNoFallbackConservadorSemMdr()
    {
        // Tenant sem NENHUMA FormaDePagamento seedada (nunca rodou o bootstrap) — o rótulo do
        // evento não bate com nada e o fold nunca inventa desconto que o lojista não configurou.
        var repositorio = new InMemoryFatoRecebiveisRepository();
        var projecao = new FatoRecebiveisProjection(repositorio, new InMemoryFormaDePagamentoRepository());

        var ocorridoEm = new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(-3));
        await AplicarAsync(projecao, new VendaConcluida("venda-desconhecida", TenantId, 10_000, "carteira-digital-nova", ocorridoEm), "venda.concluida:venda-desconhecida");

        var linha = Assert.Single(await repositorio.ListarPorVencimentoAsync(TenantId, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)));
        Assert.Equal(0m, linha.TaxaPercentualAplicada);
        Assert.Equal(10_000, linha.ValorLiquidoCentavos);
        Assert.Equal(linha.Vencimento, linha.DataLiquidacaoPrevista); // D+0
    }

    /// <summary>Semeia um <see cref="InMemoryFormaDePagamentoRepository"/> com as MESMAS taxas/lags
    /// que a antiga <c>ConfiguracaoDeRecebiveisOptions.PadraoDeMercado</c> hardcodava — mesmos
    /// números, agora vindos do domínio (ver <c>FinanceiroBootstrapSeeder</c> em produção).</summary>
    private static async Task<IFormaDePagamentoRepository> FormasDePagamentoDeMercado()
    {
        var repositorio = new InMemoryFormaDePagamentoRepository();
        await repositorio.SalvarAsync(FormaDePagamento.Criar(TenantId, "dinheiro", TipoFormaPagamento.Dinheiro, 0m, 0).Valor);
        await repositorio.SalvarAsync(FormaDePagamento.Criar(TenantId, "pix", TipoFormaPagamento.Pix, 0m, 0).Valor);
        await repositorio.SalvarAsync(FormaDePagamento.Criar(TenantId, "debito", TipoFormaPagamento.Debito, 0.0139m, 1).Valor);
        await repositorio.SalvarAsync(FormaDePagamento.Criar(TenantId, "credito", TipoFormaPagamento.Credito, 0.0349m, 30).Valor);
        await repositorio.SalvarAsync(FormaDePagamento.Criar(TenantId, "boleto", TipoFormaPagamento.Boleto, 0.02m, 2).Valor);
        return repositorio;
    }

    private static Task AplicarAsync(FatoRecebiveisProjection projecao, IIntegrationEvent evento, string chaveIdempotencia)
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
