namespace SistemaX.Modules.Abstractions.Runtime;

/// <summary>
/// Uma linha JÁ PERSISTIDA do ledger <c>integration_events</c> — a verdade histórica de que fala
/// docs/financeiro/inteligencia-arquitetura.md §3.1/ADR-0005. <see cref="Cursor"/> é o "cursor
/// sequencial" citado no plano: um inteiro monotônico (rowid autoincrement do SQLite), usado por
/// <see cref="IProjection"/>/<c>ProjectionRunner</c> para retomar um fold exatamente de onde parou
/// — nunca por <see cref="OcorridoEm"/> (timestamp de negócio, não serve de bookmark: dois eventos
/// podem ocorrer no mesmo milissegundo, e o relógio do domínio pode não ser estritamente
/// monotônico entre terminais).
/// </summary>
public sealed record IntegrationEventLedgerEntry(
    long Cursor,
    string Id,
    string Tipo,
    string TenantId,
    string PayloadJson,
    DateTimeOffset OcorridoEm,
    string ChaveIdempotencia,
    DateTimeOffset PersistidoEmUtc);

/// <summary>
/// Port do ledger append-only de eventos de integração — a peça nº 1 do plano de inteligência do
/// Financeiro (docs/financeiro/inteligencia-arquitetura.md §3.1/ADR-0005): "cada dia sem o ledger
/// é história que se perde para sempre". Implementação concreta em
/// <c>SistemaX.Infrastructure.Local.Ledger.SqliteIntegrationEventLedgerStore</c> — este port vive
/// no kernel compartilhado (não em Infrastructure.Local) porque
/// <see cref="Runtime.InProcessIntegrationEventBus"/>, que TAMBÉM vive aqui, é quem chama
/// <see cref="AppendAsync"/> — persist-then-dispatch é responsabilidade do bus, não de cada
/// módulo produtor.
///
/// SEMPRE SQLite, nunca em memória (mesmo racional de <c>IOutboxStore</c>): o ledger é a fonte de
/// verdade histórica, não um detalhe de adapter que o módulo escolhe trocar.
/// </summary>
public interface IIntegrationEventLedgerStore
{
    /// <summary>
    /// Persiste um evento. IDEMPOTENTE por <paramref name="chaveIdempotencia"/> (UNIQUE
    /// constraint na tabela): reprocessar o mesmo fato nunca duplica a linha do ledger. Retorna
    /// <c>true</c> se uma linha NOVA foi inserida (primeira vez que este fato é visto) e
    /// <c>false</c> se já existia — quem chama nunca precisa tratar duplicata como erro; o método
    /// nunca lança por causa de conflito de chave.
    /// </summary>
    Task<bool> AppendAsync(
        string tipo,
        string tenantId,
        string payloadJson,
        DateTimeOffset ocorridoEm,
        string chaveIdempotencia,
        CancellationToken ct = default);

    /// <summary>
    /// Lote ORDENADO por <see cref="IntegrationEventLedgerEntry.Cursor"/> crescente, estritamente
    /// APÓS <paramref name="afterCursor"/> (exclusive) — o método que todo fold usa para avançar
    /// de forma incremental e determinística. Lista vazia significa "nada de novo desde o
    /// cursor informado".
    /// </summary>
    Task<IReadOnlyList<IntegrationEventLedgerEntry>> LerAPartirDoCursorAsync(
        long afterCursor, int maxBatchSize, CancellationToken ct = default);

    /// <summary>Maior cursor já persistido (0 se o ledger está vazio) — usado por diagnóstico e
    /// pelo cálculo de "quão atrasada" está uma projeção.</summary>
    Task<long> ObterUltimoCursorAsync(CancellationToken ct = default);
}
