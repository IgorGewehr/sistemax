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
/// Ver <see cref="FatoCaixaDiario"/> para as limitações conhecidas desta F0 (sem MDR/lag de
/// cartão ainda — Fase 1).
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
}
