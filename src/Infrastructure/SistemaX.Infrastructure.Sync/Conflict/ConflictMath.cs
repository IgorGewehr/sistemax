namespace SistemaX.Infrastructure.Sync.Conflict;

public enum ConflictOutcome
{
    AcceptIncoming,
    KeepServer
}

/// <summary>
/// As fórmulas puras usadas por cada <see cref="ConflictStrategy"/>. Este projeto (Sync) não
/// conhece o schema concreto de "produto" ou "estoque" — quem aplica de fato a mudança na tabela
/// de negócio é um <see cref="Adapters.IRemoteChangeApplier"/> do módulo dono; ele chama estas
/// funções com os valores já extraídos do payload.
/// </summary>
public static class ConflictMath
{
    /// <summary>
    /// <see cref="ConflictStrategy.ServerWinsWithVersion"/>: servidor manda, EXCETO se o
    /// terminal carregar uma versão estritamente maior — nesse caso o terminal só pode estar
    /// certo (ex.: edição feita offline há mais tempo que o servidor conhece).
    /// </summary>
    public static ConflictOutcome ResolveByVersion(long serverVersion, long incomingVersion)
        => incomingVersion > serverVersion ? ConflictOutcome.AcceptIncoming : ConflictOutcome.KeepServer;

    /// <summary>
    /// <see cref="ConflictStrategy.ReconcileDelta"/>: em vez de <c>novoValor = valorTerminal</c>
    /// (que perderia qualquer mudança concorrente aplicada só no servidor), calcula
    /// <c>delta = valorNovoNoTerminal - valorQueOTerminalConheciaAntes</c> e SOMA esse delta ao
    /// valor atual do servidor. Replica exatamente <c>reconcileStock()</c> do Supermarket-OS
    /// (docs/robustez §3) — uma forma simplificada de CRDT de contador.
    /// </summary>
    public static decimal ReconcileByDelta(decimal serverCurrentValue, decimal terminalKnownBaseValue, decimal terminalNewValue)
    {
        var delta = terminalNewValue - terminalKnownBaseValue;
        return serverCurrentValue + delta;
    }
}
