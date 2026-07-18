using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fold determinístico do ledger para <c>fato_caixa_diario</c> — caixa REALIZADO por dia local do
/// tenant. A segunda fact table de PROVA da F0 (ver <see cref="FatoReceitaDiariaProjection"/> para
/// o racional completo do porquê da dupla receita/caixa).
///
/// Reusa <see cref="ClassificadorFormaPagamento.EhAVista"/> — a MESMA regra pura que
/// <c>VendaConcluidaHandler</c> usa pra decidir se uma venda gera <c>MovimentoFinanceiro</c>
/// imediato — porque o fold nunca lê a tabela de <c>MovimentoFinanceiro</c> diretamente (R3/
/// docs/financeiro/inteligencia-arquitetura.md: "insumo é evento, nunca leitura direta de tabela
/// alheia"); ele reconstrói o mesmo fato a partir do MESMO evento de origem.
///
/// CAIXA BILATERAL (P1-3, docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): além das entradas
/// à vista e da reversão de <c>VendaEstornada</c>, o fold agora reage a <c>ParcelaBaixada</c>
/// (publicado por <c>BaixarParcelaUseCase</c> a cada liquidação de <c>ContaAPagar</c>/
/// <c>ContaAReceber</c>) — SAÍDA de pagamento de conta (folha, compras, despesas, comissão) e
/// ENTRADA a prazo liquidada depois (ex.: recebível de cartão em D+N, já LÍQUIDO de MDR — P1-6,
/// resolvido no publicador via <c>FormaDePagamento.CalcularValorLiquido</c>, nunca recomputado
/// aqui) passam a alimentar o mesmo dia local do tenant. É esse insumo bilateral que faz as bandas
/// P5/P50/P95 (<c>BandasDeFluxoDeCaixa</c>) e o burn EWMA (<c>RunwayCalculator</c>) refletirem
/// queima de caixa de verdade — antes dele, o "ruído" histórico era quase só positivo.
/// </summary>
public sealed class FatoCaixaDiarioProjection(IFatoCaixaDiarioRepository repositorio) : IProjection
{
    public string Nome => "fato_caixa_diario";

    public Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default)
    {
        return evento.Tipo switch
        {
            nameof(VendaConcluida) => AplicarVendaConcluidaAsync(evento, ct),
            nameof(VendaEstornada) => AplicarVendaEstornadaAsync(evento, ct),
            nameof(PedidoPago) => AplicarPedidoPagoAsync(evento, ct),
            nameof(ParcelaBaixada) => AplicarParcelaBaixadaAsync(evento, ct),
            _ => Task.CompletedTask,
        };
    }

    public Task ResetarAsync(CancellationToken ct = default) => repositorio.ZerarTudoAsync(ct);

    private Task AplicarVendaConcluidaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaConcluida>(evento.PayloadJson)!;
        if (!ClassificadorFormaPagamento.EhAVista(e.FormaPagamento))
            return Task.CompletedTask; // a prazo — não vira caixa nesta data (F1: lag/D+N)

        return repositorio.AcumularEntradaAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), e.TotalCentavos, ct);
    }

    private Task AplicarVendaEstornadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaEstornada>(evento.PayloadJson)!;
        return repositorio.AcumularSaidaAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), e.TotalCentavos, ct);
    }

    private Task AplicarPedidoPagoAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<PedidoPago>(evento.PayloadJson)!;
        return repositorio.AcumularEntradaAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), e.TotalCentavos, ct);
    }

    /// <summary>Fecha o lado bilateral (P1-3): SAÍDA de ContaAPagar liquidada vira saída de caixa no
    /// dia do pagamento; ENTRADA de ContaAReceber liquidada (a prazo — a à vista já entrou via
    /// <see cref="AplicarVendaConcluidaAsync"/>/<see cref="AplicarPedidoPagoAsync"/> e não passa por
    /// <c>BaixarParcelaUseCase</c>, então não há dupla contagem) vira entrada, já LÍQUIDA de MDR
    /// quando aplicável.</summary>
    private Task AplicarParcelaBaixadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<ParcelaBaixada>(evento.PayloadJson)!;
        var dia = BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm);
        return e.EhAPagar
            ? repositorio.AcumularSaidaAsync(e.TenantId, dia, e.ValorCaixaCentavos, ct)
            : repositorio.AcumularEntradaAsync(e.TenantId, dia, e.ValorCaixaCentavos, ct);
    }
}
