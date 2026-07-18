using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;

namespace SistemaX.Modules.Financeiro.Application.EventosDeIntegracao.Handlers;

/// <summary>
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — DUNNING: liga a assinatura ao ciclo de
/// vencimento/liquidação de SUAS PRÓPRIAS cobranças. EXCEÇÃO deliberada à regra geral do módulo
/// (documentada em <c>FinanceiroModule</c>): normalmente o Financeiro não assina <c>ParcelaVencida</c>
/// porque é ele mesmo quem o publica — aqui é o único consumidor legítimo, porque a ASSINATURA
/// (agregado diferente de <c>ContaAReceber</c>) precisa reagir ao próprio recebível vencer/liquidar
/// para acionar sua FSM de dunning. Idempotente por natureza: <see cref="Assinatura.MarcarInadimplente"/>/
/// <see cref="Assinatura.Regularizar"/> recusam a transição se o estado já não bate (reentrega do
/// mesmo evento é no-op).
///
/// Resolve "de qual assinatura é essa parcela?" pelo <see cref="SourceRef"/> gravado em
/// <c>Assinatura.GerarCobranca</c> (<c>assinatura:{assinaturaId}:{yyyyMM}</c>) — nunca duplica essa
/// regra, só lê de volta.
/// </summary>
public sealed class DunningAssinaturaHandler(
    IContaAReceberRepository contasAReceber, IAssinaturaRepository assinaturas)
    : IIntegrationEventHandler<ParcelaVencida>, IIntegrationEventHandler<ParcelaBaixada>
{
    public async Task HandleAsync(ParcelaVencida evento, CancellationToken ct = default)
    {
        if (evento.EhAPagar) return; // dunning é só do lado a receber (a empresa cobrando o cliente)

        var assinaturaId = await ResolverAssinaturaIdAsync(evento.ContaId, ct).ConfigureAwait(false);
        if (assinaturaId is null) return;

        var assinatura = await assinaturas.BuscarAsync(evento.TenantId, assinaturaId, ct).ConfigureAwait(false);
        if (assinatura is null) return;

        var resultado = assinatura.MarcarInadimplente(evento.OcorridoEm);
        if (resultado.Falha) return; // já inadimplente/pausada/cancelada — nada a fazer

        await assinaturas.SalvarAsync(assinatura, ct).ConfigureAwait(false);
    }

    public async Task HandleAsync(ParcelaBaixada evento, CancellationToken ct = default)
    {
        if (evento.EhAPagar) return;

        var assinaturaId = await ResolverAssinaturaIdAsync(evento.ContaId, ct).ConfigureAwait(false);
        if (assinaturaId is null) return;

        var assinatura = await assinaturas.BuscarAsync(evento.TenantId, assinaturaId, ct).ConfigureAwait(false);
        if (assinatura is null || assinatura.Status != StatusAssinatura.Inadimplente) return;

        var resultado = assinatura.Regularizar(evento.OcorridoEm);
        if (resultado.Sucesso) await assinaturas.SalvarAsync(assinatura, ct).ConfigureAwait(false);
    }

    private async Task<string?> ResolverAssinaturaIdAsync(string contaId, CancellationToken ct)
    {
        var conta = await contasAReceber.ObterPorIdAsync(contaId, ct).ConfigureAwait(false);
        if (conta is null || conta.SourceRef.Modulo != "assinatura") return null;

        var separador = conta.SourceRef.Id.IndexOf(':');
        return separador < 0 ? conta.SourceRef.Id : conta.SourceRef.Id[..separador];
    }
}
