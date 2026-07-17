using SistemaX.Infrastructure.Sync.Model;

namespace SistemaX.Infrastructure.Sync.ChangeLog;

/// <summary>
/// Log de mudanças aceitas por este receptor — a fonte do PULL para os demais terminais. Atribui
/// <c>ServerSequence</c> (monotônico, definido pelo RECEPTOR, nunca pelo terminal de origem) e
/// preserva <c>OriginTerminalId</c> para o filtro de eco ("não recebo de volta minhas próprias mudanças").
/// </summary>
public interface IChangeLogStore
{
    /// <summary>Grava a mudança e retorna o <c>ServerSequence</c> atribuído.</summary>
    Task<long> AppendAsync(IncomingChange change, CancellationToken ct = default);

    /// <summary>
    /// Mudanças com <c>ServerSequence &gt; sinceServerSequence</c>, EXCLUINDO as originadas pelo
    /// próprio <paramref name="excludeTerminalId"/> (prevenção de eco), em ordem crescente.
    /// </summary>
    Task<IReadOnlyList<RemoteChange>> GetSinceAsync(long sinceServerSequence, string excludeTerminalId, int maxItems, CancellationToken ct = default);
}
