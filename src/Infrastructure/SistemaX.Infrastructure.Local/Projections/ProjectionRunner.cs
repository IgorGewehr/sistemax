using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions.Runtime;

namespace SistemaX.Infrastructure.Local.Projections;

/// <summary>
/// Motor de fold do ledger — a peça nº 2 do plano de inteligência do Financeiro (ver
/// docs/financeiro/inteligencia-arquitetura.md §3.2/ADR-0005). Lê <c>integration_events</c> em
/// lotes ORDENADOS por cursor, chama <see cref="IProjection.AplicarAsync"/> evento a evento, e só
/// avança <c>projection_state</c> depois que o lote inteiro foi aplicado com sucesso.
///
/// REPROCESSABILIDADE (regra dura da F0): <see cref="ReconstruirAsync"/> é "zera a fact table +
/// zera o cursor + reaplica desde seq=0" — o mesmo caminho de código de
/// <see cref="ExecutarUmaAsync"/>, só que partindo de zero. Se <see cref="IProjection.AplicarAsync"/>
/// for de fato determinística, o estado final é idêntico ao acumulado incrementalmente; é essa
/// igualdade que os testes de contrato desta F0 verificam.
/// </summary>
public sealed class ProjectionRunner(
    IIntegrationEventLedgerStore ledger,
    IProjectionStateStore estado,
    IServiceScopeFactory scopeFactory)
{
    private const int TamanhoDoLote = 500;

    /// <summary>
    /// Roda o catch-up incremental de TODAS as <see cref="IProjection"/> registradas via DI.
    /// Resolve-as dentro de um escopo PRÓPRIO e descartável (mesmo racional de
    /// <c>InProcessIntegrationEventBus.PublishAsync</c>): este runner é Singleton, mas uma
    /// projeção real (ex.: as de Financeiro) depende de um repositório SQLite Scoped
    /// (participa de <c>ILocalSessao</c>) — resolver direto no construtor deste Singleton seria
    /// um captive dependency. <see cref="ExecutarUmaAsync"/> continua aceitando uma
    /// <see cref="IProjection"/> já instanciada pra quem (testes, <see cref="ReconstruirAsync"/>)
    /// não precisa passar pelo container.
    /// </summary>
    public async Task ExecutarTudoAsync(CancellationToken ct = default)
    {
        await using var escopo = scopeFactory.CreateAsyncScope();
        foreach (var projecao in escopo.ServiceProvider.GetServices<IProjection>())
        {
            await ExecutarUmaAsync(projecao, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Avança UMA projeção do seu cursor salvo até o fim do ledger disponível.</summary>
    public async Task ExecutarUmaAsync(IProjection projecao, CancellationToken ct = default)
    {
        var cursor = await estado.ObterCursorAsync(projecao.Nome, ct).ConfigureAwait(false);

        while (true)
        {
            var lote = await ledger.LerAPartirDoCursorAsync(cursor, TamanhoDoLote, ct).ConfigureAwait(false);
            if (lote.Count == 0)
            {
                break;
            }

            foreach (var evento in lote)
            {
                await projecao.AplicarAsync(evento, ct).ConfigureAwait(false);
                cursor = evento.Cursor;
            }

            await estado.SalvarCursorAsync(projecao.Nome, cursor, ct).ConfigureAwait(false);

            if (lote.Count < TamanhoDoLote)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Reconstrói UMA projeção do zero — a correção canônica para uma fact table com bug
    /// (ADR-0005 §7: "DROP + replay"). Nunca migra o passado: apaga o estado derivado e refaz o
    /// fold inteiro a partir do ledger, que é a única fonte de verdade.
    /// </summary>
    public async Task ReconstruirAsync(IProjection projecao, CancellationToken ct = default)
    {
        await projecao.ResetarAsync(ct).ConfigureAwait(false);
        await estado.SalvarCursorAsync(projecao.Nome, 0, ct).ConfigureAwait(false);
        await ExecutarUmaAsync(projecao, ct).ConfigureAwait(false);
    }
}
