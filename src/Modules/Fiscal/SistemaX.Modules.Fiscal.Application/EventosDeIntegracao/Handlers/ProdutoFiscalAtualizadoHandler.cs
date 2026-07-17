using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Produtos;

namespace SistemaX.Modules.Fiscal.Application.EventosDeIntegracao.Handlers;

/// <summary>Mantém <see cref="IDadosFiscaisProdutoCacheRepository"/> — a cópia local
/// eventualmente consistente de Ncm/Cest/NaturezaOperacao/CfopOverride do produto (Estoque) —
/// sincronizada. Idempotente por natureza: reaplicar o mesmo evento é apenas um SET (upsert),
/// nunca um append (docs/fiscal/arquitetura.md §4).</summary>
public sealed class ProdutoFiscalAtualizadoHandler(IDadosFiscaisProdutoCacheRepository cache)
    : IIntegrationEventHandler<ProdutoFiscalAtualizado>
{
    public Task HandleAsync(ProdutoFiscalAtualizado evento, CancellationToken ct = default)
        => cache.SalvarAsync(new DadosFiscaisProdutoCache(
            evento.TenantId, evento.ProdutoId, evento.Ncm, evento.Cest,
            NaturezaOperacaoProdutoExtensions.DeCodigo(evento.NaturezaOperacao), evento.CfopOverride), ct);
}
