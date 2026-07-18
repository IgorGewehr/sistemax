using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fold determinístico do ledger para <c>fato_recebiveis</c> — a "verdade de recebíveis" que a
/// frente 3 da autonomia do motor financeiro pedia: reage a <see cref="VendaConcluida"/> (+) e
/// <see cref="PedidoPago"/> (+), calculando o valor LÍQUIDO (MDR da forma de pagamento já
/// descontado) e a data em que o dinheiro EFETIVAMENTE cai (vencimento + lag de liquidação D+N) —
/// nunca só o bruto/data de emissão. <see cref="VendaEstornada"/> (-) lança uma linha compensando
/// o BRUTO no dia do estorno.
///
/// RECONCILIADO com o domínio Bancário (docs/wiring/financeiro-telas-restantes.md §3): MDR/lag
/// vêm de <see cref="IFormaDePagamentoRepository.ObterPorNomeAsync"/> — o rótulo livre do evento
/// (<c>"pix"</c>, <c>"credito"</c>, ...) é resolvido contra a <c>FormaDePagamento</c> cadastrada do
/// tenant. A antiga <c>ConfiguracaoDeRecebiveisOptions</c> (config estática paralela ao domínio) foi
/// REMOVIDA — <c>FormaDePagamento</c> é agora o LAR ÚNICO da taxa/lag; os mesmos números de mercado
/// que a config hardcodava nascem como dado real via <c>FinanceiroBootstrapSeeder</c> (idempotente).
/// Forma não encontrada (tenant ainda não seedado, ou rótulo desconhecido) cai no MESMO fallback
/// conservador de sempre: a prazo, SEM taxa — nunca inventa desconto que o lojista não configurou.
///
/// <see cref="OsFaturada"/> (P1-7, docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): o evento
/// agora carrega <see cref="OsFaturada.FormaPagamento"/>, então ganha o MESMO tratamento de
/// <see cref="VendaConcluida"/>/<see cref="PedidoPago"/> — MDR/lag resolvidos pela forma informada.
/// <c>FormaPagamento</c> nulo (taxa de diagnóstico a prazo de <c>OrdemDeServico.DevolverSemReparo</c>)
/// cai no MESMO fallback conservador de sempre (0%, D+0) — nunca inventa prazo que a Assistência
/// não informou.
///
/// SPLIT DE PAGAMENTO (P2-2, docs/financeiro/revisao-domain-fit-cnpj.md — FECHADO): quando
/// <see cref="VendaConcluida.Pagamentos"/> vem com 2+ linhas (metade dinheiro, metade crédito, por
/// exemplo), o MDR/lag antes era resolvido pela forma PRINCIPAL e aplicado ao TOTAL — uma linha
/// paga em dinheiro (0% de taxa) "emprestava" a taxa zero pro resto pago no crédito, ou o inverso.
/// Agora cada pagamento vira sua PRÓPRIA linha de <c>fato_recebiveis</c>, com o MDR/lag da SUA
/// forma — <c>Σ ValorBrutoCentavos</c> das linhas continua batendo com <c>TotalCentavos</c>
/// (conservação), mas o líquido total agora reflete a taxa correta de CADA forma, não uma média
/// distorcida pela forma principal. Com 0 ou 1 pagamento (o caso comum hoje), o comportamento é
/// IDÊNTICO ao de sempre — uma linha só, pela <see cref="VendaConcluida.FormaPagamento"/> principal.
/// </summary>
public sealed class FatoRecebiveisProjection(
    IFatoRecebiveisRepository repositorio, IFormaDePagamentoRepository formasDePagamento) : IProjection
{
    public string Nome => "fato_recebiveis";

    public Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default)
    {
        return evento.Tipo switch
        {
            nameof(VendaConcluida) => AplicarVendaConcluidaAsync(evento, ct),
            nameof(PedidoPago) => AplicarPedidoPagoAsync(evento, ct),
            nameof(VendaEstornada) => AplicarVendaEstornadaAsync(evento, ct),
            nameof(OsFaturada) => AplicarOsFaturadaAsync(evento, ct),
            _ => Task.CompletedTask,
        };
    }

    public Task ResetarAsync(CancellationToken ct = default) => repositorio.ZerarTudoAsync(ct);

    private Task AplicarVendaConcluidaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaConcluida>(evento.PayloadJson)!;

        // P2-2 — split real (2+ pagamentos): uma linha de fato_recebiveis POR PAGAMENTO, cada uma
        // com o MDR/lag da SUA forma. 0 ou 1 pagamento: comportamento de sempre (uma linha, forma
        // principal, chave "sale:{VendaId}" preservada — nenhum consumidor existente quebra).
        if (e.Pagamentos is { Count: > 1 } pagamentos)
        {
            return RegistrarSplitAsync(e.TenantId, e.VendaId, pagamentos, e.OcorridoEm, ct);
        }
        return RegistrarAsync(e.TenantId, $"sale:{e.VendaId}", e.FormaPagamento, e.TotalCentavos, e.OcorridoEm, ct);
    }

    private async Task RegistrarSplitAsync(
        string tenantId, string vendaId, IReadOnlyList<PagamentoIntegracao> pagamentos, DateTimeOffset ocorridoEm, CancellationToken ct)
    {
        for (var indice = 0; indice < pagamentos.Count; indice++)
        {
            var pagamento = pagamentos[indice];
            await RegistrarAsync(tenantId, $"sale:{vendaId}:{indice}", pagamento.Metodo, pagamento.ValorCentavos, ocorridoEm, ct)
                .ConfigureAwait(false);
        }
    }

    private Task AplicarPedidoPagoAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<PedidoPago>(evento.PayloadJson)!;
        return RegistrarAsync(e.TenantId, $"order:{e.PedidoId}", e.FormaPagamento, e.TotalCentavos, e.OcorridoEm, ct);
    }

    private Task AplicarOsFaturadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<OsFaturada>(evento.PayloadJson)!;
        var total = e.ValorServicoCentavos + e.ValorPecasCentavos;
        return RegistrarAsync(e.TenantId, $"os:{e.OrdemServicoId}", e.FormaPagamento, total, e.OcorridoEm, ct);
    }

    /// <summary>
    /// Reversão: o evento não carrega a forma de pagamento original, então a linha compensatória
    /// nasce SEM taxa (líquido = bruto, D+0 no dia do estorno) — o valor relevante aqui é sinalizar
    /// "esse dinheiro não vem mais" no bucket de vencimento correto, não reconciliar centavo a
    /// centavo com o extrato da adquirente (isso é papel da conciliação bancária, módulo à parte).
    /// </summary>
    private Task AplicarVendaEstornadaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaEstornada>(evento.PayloadJson)!;
        return RegistrarAsync(e.TenantId, $"sale-reversal:{e.VendaId}", formaPagamento: null, -e.TotalCentavos, e.OcorridoEm, ct);
    }

    private async Task RegistrarAsync(
        string tenantId, string origemChave, string? formaPagamento, long valorBrutoCentavos, DateTimeOffset ocorridoEm, CancellationToken ct)
    {
        var (taxaPercentual, lagDias) = formaPagamento is null
            ? (0m, 0)
            : await ResolverTaxaELagAsync(tenantId, formaPagamento, ct).ConfigureAwait(false);

        // Taxa sempre calculada sobre o valor ABSOLUTO (nunca inverte sinal por causa de um MDR
        // "negativo") e aplicada na mesma direção do bruto — uma reversão negativa gera líquido
        // negativo de magnitude menor (nunca maior) que o bruto, mesma relação da linha original.
        var taxaCentavos = (long)Math.Round(Math.Abs(valorBrutoCentavos) * taxaPercentual, MidpointRounding.ToEven);
        var valorLiquidoCentavos = valorBrutoCentavos >= 0 ? valorBrutoCentavos - taxaCentavos : valorBrutoCentavos + taxaCentavos;

        var vencimento = BucketingTemporalDoTenant.DiaLocal(ocorridoEm);
        var dataLiquidacaoPrevista = vencimento.AddDays(lagDias);

        await repositorio.AdicionarAsync(new FatoRecebivel(
            TenantId: tenantId,
            OrigemChave: origemChave,
            Vencimento: vencimento,
            DataLiquidacaoPrevista: dataLiquidacaoPrevista,
            FormaPagamento: formaPagamento,
            TaxaPercentualAplicada: taxaPercentual,
            ValorBrutoCentavos: valorBrutoCentavos,
            ValorLiquidoCentavos: valorLiquidoCentavos,
            AtualizadoEmUtc: DateTimeOffset.UtcNow), ct).ConfigureAwait(false);
    }

    /// <summary>Resolve taxa/lag consultando a <c>FormaDePagamento</c> cadastrada do tenant pelo
    /// rótulo do evento (case-insensitive). Não encontrada → fallback conservador (0%, D+0): trata
    /// como recebimento à vista sem desconto, nunca inventa um MDR que ninguém configurou.</summary>
    private async Task<(decimal TaxaPercentual, int LagDias)> ResolverTaxaELagAsync(string tenantId, string formaPagamento, CancellationToken ct)
    {
        var forma = await formasDePagamento.ObterPorNomeAsync(tenantId, formaPagamento, ct).ConfigureAwait(false);
        return forma is null ? (0m, 0) : (forma.TaxaPercentual, forma.PrazoCompensacaoDias);
    }
}
