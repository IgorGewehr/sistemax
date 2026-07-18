using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;

namespace SistemaX.Modules.Financeiro.Application.Mrr;

/// <summary>
/// PONTE ÚNICA entre os eventos de domínio de <see cref="Assinatura"/> e o ledger de
/// <see cref="MovimentoMrr"/> (P1-4, docs/financeiro/revisao-domain-fit-cnpj.md) — todo caso de uso
/// que muta uma assinatura (criar/cancelar/pausar/reativar/alterar valor, inclusive o cron de
/// dunning) chama <see cref="RegistrarTodosAsync"/> logo depois de persistir, lendo os
/// <c>DomainEvents</c> recém-levantados. Um lar único: nunca duplicar, em cada caso de uso, a
/// decisão de "qual evento vira qual tipo de movimento".
/// </summary>
public static class RegistradorDeMovimentoMrr
{
    public static async Task RegistrarTodosAsync(Assinatura assinatura, IMovimentoMrrRepository movimentos, CancellationToken ct)
    {
        foreach (var evento in assinatura.DomainEvents)
        {
            var movimento = evento switch
            {
                AssinaturaCriada e => MovimentoMrr.Novo(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrCentavos, e.Inicio),

                // Churn com magnitude 0 (assinatura já estava Pausada — ver Assinatura.Cancelar)
                // não é um movimento: o MRR já tinha saído via Contração na pausa.
                AssinaturaCancelada e when e.MrrCentavos > 0 => MovimentoMrr.Churn(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrCentavos, e.Quando),

                AssinaturaPausada e when e.MrrCentavos > 0 => MovimentoMrr.Contracao(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrCentavos, e.Quando),

                AssinaturaReativada e when e.MrrCentavos > 0 => MovimentoMrr.Reativacao(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrCentavos, e.Quando),

                AssinaturaAlterada e when e.MrrNovoCentavos > e.MrrAnteriorCentavos =>
                    MovimentoMrr.Expansao(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrNovoCentavos - e.MrrAnteriorCentavos, e.Quando),

                AssinaturaAlterada e when e.MrrNovoCentavos < e.MrrAnteriorCentavos =>
                    MovimentoMrr.Contracao(e.BusinessId, e.AssinaturaId, e.ServicoId, e.MrrAnteriorCentavos - e.MrrNovoCentavos, e.Quando),

                _ => null
            };

            if (movimento is not null) await movimentos.RegistrarAsync(movimento, ct).ConfigureAwait(false);
        }

        assinatura.ClearDomainEvents();
    }
}
