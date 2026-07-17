namespace SistemaX.Infrastructure.Local.Recovery;

/// <summary>
/// Levantado depois de uma recuperação automática de corrupção — o composition root do host
/// assina este evento para alertar o admin (nunca é um erro "silencioso"; ver docs/robustez §2).
/// </summary>
public sealed class CorruptionRecoveredEventArgs(string corruptedFilePreservedAt, string? restoredFromBackup) : EventArgs
{
    /// <summary>Caminho para onde o arquivo corrompido original foi renomeado (nunca apagado — preservado para forense).</summary>
    public string CorruptedFilePreservedAt { get; } = corruptedFilePreservedAt;

    /// <summary>Caminho do backup restaurado, ou <c>null</c> se não havia backup (fail-open com banco novo/vazio).</summary>
    public string? RestoredFromBackup { get; } = restoredFromBackup;
}
