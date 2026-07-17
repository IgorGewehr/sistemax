namespace SistemaX.Infrastructure.Local.Recovery;

/// <summary>
/// Detecção e recuperação de corrupção do banco local. A filosofia é FAIL-OPEN: preferir reabrir
/// com banco restaurado (ou vazio, na ausência de backup) a travar o PDV indefinidamente — a
/// loja continua vendendo, o incidente é logado/alertável, nunca é decisão de parar a venda
/// (ver docs/robustez §2).
/// </summary>
public interface ICorruptionRecoveryService
{
    /// <summary>Levantado sempre que <see cref="AttemptRecoveryAsync"/> completa com sucesso.</summary>
    event EventHandler<CorruptionRecoveredEventArgs>? CorruptionRecovered;

    /// <summary>
    /// Roda no BOOT: <c>PRAGMA integrity_check</c> completo, mas só se já se passou
    /// <see cref="LocalDatabaseOptions.IntegrityCheckInterval"/> desde a última execução (é caro,
    /// ~500ms+; não vale rodar em todo restart). Se detectar corrupção, chama
    /// <see cref="AttemptRecoveryAsync"/> automaticamente. Retorna <c>true</c> se uma recuperação
    /// foi executada.
    /// </summary>
    Task<bool> EnsureIntegrityOnBootAsync(CancellationToken ct = default);

    /// <summary>
    /// <c>PRAGMA quick_check</c> — mais leve que o completo, pensado para rodar periodicamente
    /// EM BACKGROUND durante a operação do dia (não só no boot — fraqueza corrigida do
    /// Supermarket-OS, que só verificava corrupção ao abrir o app). Se detectar problema, chama
    /// <see cref="AttemptRecoveryAsync"/>. Retorna <c>true</c> se uma recuperação foi executada.
    /// </summary>
    Task<bool> RunQuickCheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Sequência exata: (1) fecha conexões pooladas, (2) renomeia o arquivo corrompido (+ -wal/-shm)
    /// para <c>.corrupted-{timestamp}</c> — NUNCA apaga, vira forense — (3) procura o backup mais
    /// recente e copia de volta para o caminho original, (4) se não houver backup, segue com
    /// banco novo/vazio (fail-open), (5) garante que o schema de infraestrutura existe de novo.
    /// </summary>
    Task AttemptRecoveryAsync(CancellationToken ct = default);
}
