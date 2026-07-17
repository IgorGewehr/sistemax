using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Eventos;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// O "cron financeiro" (docs/financeiro-datamodel.md §4.2, linha <c>parcela.vencida</c>): note
/// que, pelo próprio catálogo da spec, a ORIGEM desse evento é "Cron financeiro" — ou seja, este
/// caso de uso é quem PUBLICA <see cref="ParcelaVencida"/> como evento de integração para outros
/// módulos (ex.: CRM disparar régua de cobrança), não um handler que o consome. Isso é uma
/// inversão deliberada do papel usual do Financeiro nesta spec (consumidor na maioria dos outros
/// eventos, produtor apenas deste) — documentada aqui e no README do módulo.
///
/// Idempotente por natureza (não por execução): reavalia o estado ATUAL a cada chamada. Rodar
/// duas vezes no mesmo dia não duplica publicação — a segunda rodada já encontra a parcela em
/// 'Atrasado' (o domain event só é levantado na transição, nunca ao reafirmar um estado já atingido).
/// </summary>
public sealed class AvaliarParcelasVencidasUseCase(
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IIntegrationEventBus barramentoDeEventos,
    IRelogio relogio)
{
    public async Task<int> ExecutarAsync(string businessId, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var publicados = 0;

        foreach (var conta in await contasAReceber.ListarAbertasAteAsync(businessId, agora, ct))
        {
            var resultado = conta.AvaliarVencimento(agora);
            if (resultado.Falha) continue;

            publicados += await PublicarEventosDeVencimentoAsync(businessId, conta.DomainEvents, ct);
            conta.ClearDomainEvents();
            await contasAReceber.SalvarAsync(conta, ct);
        }

        foreach (var conta in await contasAPagar.ListarAbertasAteAsync(businessId, agora, ct))
        {
            var resultado = conta.AvaliarVencimento(agora);
            if (resultado.Falha) continue;

            publicados += await PublicarEventosDeVencimentoAsync(businessId, conta.DomainEvents, ct);
            conta.ClearDomainEvents();
            await contasAPagar.SalvarAsync(conta, ct);
        }

        return publicados;
    }

    private async Task<int> PublicarEventosDeVencimentoAsync(string businessId, IReadOnlyList<SistemaX.SharedKernel.IDomainEvent> eventos, CancellationToken ct)
    {
        var publicados = 0;
        foreach (var evento in eventos.OfType<ParcelaMarcadaVencida>())
        {
            await barramentoDeEventos.PublishAsync(
                new ParcelaVencida(evento.ParcelaId, businessId, evento.ValorCentavos, evento.EhAPagar, evento.OccurredOn), ct);
            publicados++;
        }
        return publicados;
    }
}
