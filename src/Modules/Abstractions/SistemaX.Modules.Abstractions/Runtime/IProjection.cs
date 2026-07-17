namespace SistemaX.Modules.Abstractions.Runtime;

/// <summary>
/// Uma PROJEÇÃO: fold determinístico do ledger <c>integration_events</c> para uma fact table
/// própria (ex.: <c>fato_receita_diaria</c>, <c>fato_caixa_diario</c>) — a peça nº 2 do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md §3.2/ADR-0005).
///
/// Cada módulo registra a(s) sua(s) via DI (<c>IEnumerable&lt;IProjection&gt;</c>) — mesma
/// filosofia de <c>IModuleSchemaMigration</c>/<c>IIntegrationEventHandler{T}</c>: o runner
/// (<c>SistemaX.Infrastructure.Local.Projections.ProjectionRunner</c>) descobre todas via DI e
/// nunca conhece uma concreta.
///
/// CONTRATO DE REPROCESSABILIDADE (regra dura da F0): reconstruir uma projeção do ZERO
/// (<see cref="ResetarAsync"/> seguido de reaplicar TODO o ledger desde o cursor 0) precisa
/// produzir EXATAMENTE o mesmo estado que o fold incremental já tinha acumulado. Isso só é
/// verdade se <see cref="AplicarAsync"/> for uma função determinística de
/// (estado acumulado, evento) → novo estado — nunca lida com relógio de parede, aleatoriedade ou
/// I/O externo dentro do fold.
/// </summary>
public interface IProjection
{
    /// <summary>Nome estável — chave em <c>projection_state</c>. Nunca muda depois de rodar em
    /// produção (mudar o nome reseta o cursor da projeção, forçando um replay completo).</summary>
    string Nome { get; }

    /// <summary>
    /// Aplica UM evento do ledger ao estado acumulado da projeção. Chamado pelo runner em ORDEM de
    /// <see cref="IntegrationEventLedgerEntry.Cursor"/> estritamente crescente, nunca pulando um
    /// evento nem processando fora de ordem. Eventos de tipos que esta projeção não conhece devem
    /// ser ignorados silenciosamente (o fold é seletivo — cada projeção só reage aos tipos que lhe
    /// interessam).
    /// </summary>
    Task AplicarAsync(IntegrationEventLedgerEntry evento, CancellationToken ct = default);

    /// <summary>
    /// Apaga TODO o estado acumulado desta projeção (a fact table inteira) — o primeiro passo de
    /// um replay do zero (ADR-0005 §7: "fact table com bug se corrige com DROP + replay").
    /// Idempotente: chamar numa fact table já vazia é no-op.
    /// </summary>
    Task ResetarAsync(CancellationToken ct = default);
}
