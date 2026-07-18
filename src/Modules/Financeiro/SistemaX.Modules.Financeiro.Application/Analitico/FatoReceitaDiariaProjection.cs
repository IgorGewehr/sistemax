using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fold determinístico do ledger para <c>fato_receita_diaria</c> — receita RECONHECIDA
/// (competência) por dia local do tenant. Uma das duas fact tables de PROVA da F0 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005): prova que
/// ledger→fold→fact table funciona ponta a ponta antes das análises quant da F1.
///
/// Reage a <see cref="VendaConcluida"/> (+), <see cref="VendaEstornada"/> (-),
/// <see cref="PedidoPago"/> (+), <see cref="OsFaturada"/> (+ serviço + peças) e
/// <see cref="CobrancaDeAssinaturaGerada"/> (+, corrente Recorrente — P0-4: fecha o gap de RBT12
/// não incluir assinaturas) — os cinco eventos do catálogo que já representam receita reconhecida
/// hoje. Qualquer outro tipo de evento do ledger é ignorado silenciosamente (o fold é seletivo por
/// natureza).
/// </summary>
public sealed class FatoReceitaDiariaProjection(IFatoReceitaDiariaRepository repositorio) : IProjection
{
    public string Nome => "fato_receita_diaria";

    public Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default)
    {
        return evento.Tipo switch
        {
            nameof(VendaConcluida) => AplicarVendaConcluidaAsync(evento, ct),
            nameof(VendaEstornada) => AplicarVendaEstornadaAsync(evento, ct),
            nameof(PedidoPago) => AplicarPedidoPagoAsync(evento, ct),
            nameof(OsFaturada) => AplicarOsFaturadaAsync(evento, ct),
            nameof(CobrancaDeAssinaturaGerada) => AplicarCobrancaDeAssinaturaGeradaAsync(evento, ct),
            _ => Task.CompletedTask,
        };
    }

    public Task ResetarAsync(CancellationToken ct = default) => repositorio.ZerarTudoAsync(ct);

    private Task AplicarVendaConcluidaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaConcluida>(evento.PayloadJson)!;
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Comercio, e.TotalCentavos, ct: ct);
    }

    private Task AplicarVendaEstornadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaEstornada>(evento.PayloadJson)!;
        // Reduz a receita no dia do ESTORNO (não retroage ao dia original — o fold é incremental,
        // nunca reabre um dia já fechado; mesmo racional de LancamentoContabil.GerarEstorno).
        // Corrente: venda é sempre Comercio (P0-1) — o estorno reverte na mesma corrente.
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Comercio, -e.TotalCentavos, ct: ct);
    }

    private Task AplicarPedidoPagoAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<PedidoPago>(evento.PayloadJson)!;
        // Corrente: pedido (delivery/balcão) é venda de produto — Comercio (P0-1).
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Comercio, e.TotalCentavos, ct: ct);
    }

    private Task AplicarOsFaturadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<OsFaturada>(evento.PayloadJson)!;
        var total = e.ValorServicoCentavos + e.ValorPecasCentavos;
        // Corrente: OS é sempre Servico (P0-1) — mão de obra + peças ainda somadas num único
        // número aqui (a repartição interna é P0-2/Fatia 3, quando o evento ganhar os dois campos
        // separados no fold — hoje só o handler de ContaAReceber já separa os valores na origem).
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Servico, total, ct: ct);
    }

    private Task AplicarCobrancaDeAssinaturaGeradaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<CobrancaDeAssinaturaGerada>(evento.PayloadJson)!;
        // Corrente: cobrança de assinatura é sempre Recorrente (P0-1/P0-4) — fecha o gap "RBT12 não
        // inclui assinaturas" (docs/financeiro/revisao-domain-fit-cnpj.md P0-4).
        // ProjetoId (P5, docs/financeiro/design-analise-por-projeto.md §11) — a ÚNICA fonte que já
        // carrega a dimensão real hoje; sentinela "" quando a assinatura não está tageada.
        return repositorio.AcumularAsync(
            e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Recorrente, e.ValorCentavos, e.ProjetoId ?? "", ct);
    }
}
