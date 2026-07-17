using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Runtime;
using SistemaX.Modules.Financeiro.Application.Comum;
using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.Analitico;

/// <summary>
/// Fold determinístico do ledger para <c>fato_margem_produto</c> — a fact table por PRODUTO da F1
/// do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md §6/ADR-0005,
/// "motor base": #16 sazonalidade e #6 MC por item). Ver <see cref="FatoMargemProduto"/> para a
/// motivação completa (por que CMV via rateio, e a limitação conhecida de estorno).
///
/// ORDEM DE CHEGADA DOS EVENTOS (garantida pelo bus persist-then-dispatch — ver
/// <c>Runtime/InProcessIntegrationEventBus</c>): <c>VendaConcluida</c> é publicado primeiro; o
/// Financeiro assina e não faz nada aqui (esta projeção não reage a ele). Em seguida
/// <c>VendaItensMovimentados</c> é publicado — <c>VendaItensMovimentadosHandler</c> (Estoque)
/// PROCESSA esse evento (baixando estoque item a item) e, ao final da MESMA chamada, publica
/// <c>CustoBaixadoPorVenda</c> — que por ser um <c>PublishAsync</c> ANINHADO dentro do dispatch de
/// <c>VendaItensMovimentados</c>, recebe um cursor de ledger MAIOR (é persistido depois). Ou seja:
/// para a MESMA venda, <c>VendaItensMovimentados</c> sempre chega a este fold ANTES de
/// <c>CustoBaixadoPorVenda</c> — é essa garantia de ordem que permite ao fold usar
/// <see cref="IFatoMargemProdutoRepository.RegistrarItensDeVendaAsync"/> como "abre a transição" e
/// <see cref="IFatoMargemProdutoRepository.AlocarCustoDeVendaAsync"/> como "fecha a transição" sem
/// nunca precisar esperar/reordenar.
/// </summary>
public sealed class FatoMargemProdutoProjection(IFatoMargemProdutoRepository repositorio) : IProjection
{
    public string Nome => "fato_margem_produto";

    public Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default)
    {
        return evento.Tipo switch
        {
            nameof(VendaItensMovimentados) => AplicarVendaItensMovimentadosAsync(evento, ct),
            nameof(CustoBaixadoPorVenda) => AplicarCustoBaixadoPorVendaAsync(evento, ct),
            _ => Task.CompletedTask,
        };
    }

    public Task ResetarAsync(CancellationToken ct = default) => repositorio.ZerarTudoAsync(ct);

    private Task AplicarVendaItensMovimentadosAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<VendaItensMovimentados>(evento.PayloadJson)!;
        var dia = BucketingTemporalDoTenant.DiaLocal(e.OcorridoEm);

        // Agrega por produto (uma venda pode ter mais de uma linha do mesmo produto) — a receita
        // do item é preço×quantidade menos desconto, a MESMA regra que Vendas usa para compor o
        // total da venda (ver ItemMovimentado em Modules.Abstractions/IntegrationEvents.cs).
        var porProduto = new Dictionary<string, long>();
        foreach (var item in e.Itens)
        {
            var receitaItem = ReceitaDoItem(item);
            porProduto[item.ProdutoId] = porProduto.GetValueOrDefault(item.ProdutoId, 0) + receitaItem;
        }

        var itensPendentes = porProduto.Select(kv => new ItemMargemPendente(kv.Key, kv.Value)).ToList();
        return repositorio.RegistrarItensDeVendaAsync(e.TenantId, e.VendaId, dia, itensPendentes, ct);
    }

    private Task AplicarCustoBaixadoPorVendaAsync(IntegrationEventLedgerEntry evento, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<CustoBaixadoPorVenda>(evento.PayloadJson)!;
        return repositorio.AlocarCustoDeVendaAsync(e.TenantId, e.VendaId, e.CustoTotalCentavos, ct);
    }

    private static long ReceitaDoItem(ItemMovimentado item)
        => item.PrecoUnitarioCentavos * item.QuantidadeMilesimos / 1000 - item.DescontoCentavos;
}
