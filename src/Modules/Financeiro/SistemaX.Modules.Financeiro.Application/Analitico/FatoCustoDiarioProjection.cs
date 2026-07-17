using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fold determinístico do ledger para <c>fato_custo_diario</c> — o fold que fecha o gap
/// documentado no plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md
/// §3.3/ADR-0005): <see cref="CustoBaixadoPorVenda"/> era publicado pelo Estoque e persistia no
/// ledger, mas nenhuma fact table reagia — o CMV entrava no ledger e não chegava a nenhuma tabela
/// consultável. Ver <see cref="FatoCustoDiario"/> para o racional de ser tabela própria (não coluna
/// em <see cref="FatoReceitaDiaria"/>).
///
/// P0-5 (docs/financeiro/revisao-domain-fit-cnpj.md): <see cref="CustoBaixadoPorOs"/> é o
/// companion da corrente Servico — o CMV da peça consumida numa OS (baixa e estorno já chegam
/// com o SINAL correto embutido, ver <c>PecaConsumidaHandler</c>/<c>ConsumoEstornadoHandler</c> do
/// Estoque) soma direto no bucket (tenant, dia, Servico), sem precisar de lógica de sinal aqui.
/// </summary>
public sealed class FatoCustoDiarioProjection(IFatoCustoDiarioRepository repositorio) : IProjection
{
    public string Nome => "fato_custo_diario";

    public Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default)
    {
        return evento.Tipo switch
        {
            nameof(CustoBaixadoPorVenda) => AplicarCustoBaixadoPorVendaAsync(evento, ct),
            nameof(CustoBaixadoPorOs) => AplicarCustoBaixadoPorOsAsync(evento, ct),
            _ => Task.CompletedTask,
        };
    }

    public Task ResetarAsync(CancellationToken ct = default) => repositorio.ZerarTudoAsync(ct);

    private Task AplicarCustoBaixadoPorVendaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<CustoBaixadoPorVenda>(evento.PayloadJson)!;
        // Corrente: CMV de venda é sempre Comercio (P0-1).
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Comercio, e.CustoTotalCentavos, ct);
    }

    private Task AplicarCustoBaixadoPorOsAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<CustoBaixadoPorOs>(evento.PayloadJson)!;
        // Corrente: CMV de peça aplicada em OS é sempre Servico (P0-1); o sinal (positivo na
        // baixa, negativo no estorno) já vem embutido em CustoTotalCentavos.
        return repositorio.AcumularAsync(e.TenantId, BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm), CorrenteDeReceita.Servico, e.CustoTotalCentavos, ct);
    }
}
