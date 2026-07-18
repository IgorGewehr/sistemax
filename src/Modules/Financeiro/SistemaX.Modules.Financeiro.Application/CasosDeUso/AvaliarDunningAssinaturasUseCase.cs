using SistemaX.Modules.Financeiro.Application.Mrr;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// P1-4 (docs/financeiro/revisao-domain-fit-cnpj.md) — o lado "graça expira ⇒ churn" da política de
/// dunning: assinaturas em <see cref="StatusAssinatura.Inadimplente"/> há mais de
/// <paramref name="diasGraca"/> (contado de <see cref="Assinatura.InadimplenteDesde"/>) são
/// canceladas — dunning que não se resolveu vira churn de verdade, com o MRR movement
/// correspondente (via <see cref="RegistradorDeMovimentoMrr"/>, mesmo caminho de
/// <see cref="CancelarAssinaturaUseCase"/>).
///
/// O lado "marca Inadimplente"/"regulariza" é reativo a eventos (<c>DunningAssinaturaHandler</c>
/// consumindo <c>ParcelaVencida</c>/<c>ParcelaBaixada</c>) — este caso de uso só cuida do RELÓGIO,
/// rodado periodicamente pelo cron (mesmo molde de <c>AvaliarParcelasVencidasUseCase</c>:
/// idempotente por REAVALIAÇÃO do estado atual, não por execução — rodar 2× no mesmo dia não
/// cancela duas vezes, a segunda rodada já encontra <see cref="StatusAssinatura.Cancelada"/> e
/// <see cref="Assinatura.Cancelar"/> recusa).
/// </summary>
public sealed class AvaliarDunningAssinaturasUseCase(IAssinaturaRepository assinaturas, IMovimentoMrrRepository movimentosMrr)
{
    public async Task<int> ExecutarAsync(string businessId, DateTimeOffset agora, int diasGraca, CancellationToken ct = default)
    {
        var canceladas = 0;
        foreach (var assinatura in await assinaturas.ListarAsync(businessId, ct).ConfigureAwait(false))
        {
            if (assinatura.Status != StatusAssinatura.Inadimplente) continue;
            if (assinatura.InadimplenteDesde is not { } desde) continue;
            if (agora - desde < TimeSpan.FromDays(diasGraca)) continue;

            var resultado = assinatura.Cancelar($"dunning: {diasGraca} dia(s) de graça expirados sem regularização", agora);
            if (resultado.Falha) continue; // guarda-chuva: reavaliação de um estado que já mudou por outro caminho

            await assinaturas.SalvarAsync(assinatura, ct).ConfigureAwait(false);
            await RegistradorDeMovimentoMrr.RegistrarTodosAsync(assinatura, movimentosMrr, ct).ConfigureAwait(false);
            canceladas++;
        }
        return canceladas;
    }
}
