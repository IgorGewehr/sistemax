using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Application.EventosDeIntegracao.Handlers;

/// <summary>Companion do preenchimento de NCM em massa do Estoque (§4) — um evento por LOTE;
/// aplica um upsert por linha na mesma cópia local de <see cref="ProdutoFiscalAtualizadoHandler"/>.</summary>
public sealed class ProdutoFiscalAtualizadoEmLoteHandler(IDadosFiscaisProdutoCacheRepository cache)
    : IIntegrationEventHandler<ProdutoFiscalAtualizadoEmLote>
{
    public async Task HandleAsync(ProdutoFiscalAtualizadoEmLote evento, CancellationToken ct = default)
    {
        foreach (var (produtoId, ncm, cest, naturezaOperacao, cfopOverride) in evento.Itens)
        {
            await cache.SalvarAsync(new DadosFiscaisProdutoCache(
                evento.TenantId, produtoId, ncm, cest,
                NaturezaOperacaoProdutoExtensions.DeCodigo(naturezaOperacao), cfopOverride), ct);
        }
    }
}
