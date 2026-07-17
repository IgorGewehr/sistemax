namespace SistemaX.Infrastructure.Local;

/// <summary>
/// Configuração do banco local SQLite de UM terminal (PDV, servidor de loja ou processo de
/// desenvolvimento). Cada processo tem seu próprio arquivo — não há SQLite compartilhado por
/// rede; o compartilhamento entre terminais é feito pela camada de Sync, nunca pelo arquivo.
/// </summary>
public sealed class LocalDatabaseOptions
{
    /// <summary>Nome da seção de configuração para binding via <c>IConfiguration</c>.</summary>
    public const string SectionName = "SistemaX:Local";

    /// <summary>Caminho absoluto do arquivo .db principal. Os arquivos -wal/-shm ficam ao lado.</summary>
    public string DatabasePath { get; set; } = DefaultDatabasePath();

    /// <summary>Diretório onde os backups rotativos são gravados (7 mais recentes, ver <see cref="MaxBackups"/>).</summary>
    public string BackupDirectory { get; set; } = DefaultBackupDirectory();

    /// <summary>Quantidade de backups a manter. O restante é apagado; o arquivo corrompido NUNCA é apagado (vira forense).</summary>
    public int MaxBackups { get; set; } = 7;

    /// <summary>Intervalo mínimo entre execuções de <c>PRAGMA integrity_check</c> completo (caro; ~500ms+).</summary>
    public TimeSpan IntegrityCheckInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Intervalo de um <c>PRAGMA quick_check</c> leve rodado em background durante a operação do
    /// dia — corrige a fraqueza do Supermarket-OS de só checar corrupção no boot (ver docs/robustez §2).
    /// </summary>
    public TimeSpan QuickCheckInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Intervalo entre execuções PERIÓDICAS do catch-up de projeções (F1 do plano de inteligência
    /// do Financeiro — docs/financeiro/inteligencia-arquitetura.md §6/ADR-0005: "F1 pode evoluir
    /// isto para rodar periodicamente"). Antes da F1, o catch-up só rodava no boot — fact tables
    /// (<c>fato_receita_diaria</c>, <c>fato_margem_produto</c>, ...) ficavam atrasadas até o próximo
    /// restart do processo. Default curto (30s) porque <see cref="Projections.ProjectionRunner"/>
    /// é barato quando não há evento novo no ledger (uma query por projeção, sem trabalho).
    /// </summary>
    public TimeSpan ProjectionCatchUpInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary><c>PRAGMA busy_timeout</c> — evita erro imediato "database is locked" sob contenção entre conexões do mesmo processo.</summary>
    public TimeSpan BusyTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary><c>PRAGMA cache_size</c> em KB (negativo = KB, não páginas). Default 64MB.</summary>
    public int CacheSizeKb { get; set; } = 64_000;

    /// <summary><c>PRAGMA mmap_size</c> em bytes. Default 256MB.</summary>
    public long MmapSizeBytes { get; set; } = 256L * 1024 * 1024;

    /// <summary><c>PRAGMA journal_size_limit</c> em bytes — teto do arquivo -wal antes de forçar checkpoint.</summary>
    public long JournalSizeLimitBytes { get; set; } = 16L * 1024 * 1024;

    /// <summary>
    /// Espaço livre mínimo em disco (bytes) para considerar um backup "seguro". Abaixo disso o
    /// <see cref="Backup.IBackupManager"/> recusa o backup e sinaliza alerta em vez de falhar
    /// silenciosamente (fraqueza corrigida do Supermarket-OS, ver docs/robustez §2).
    /// </summary>
    public long MinFreeDiskSpaceBytesForBackup { get; set; } = 200L * 1024 * 1024;

    private static string DefaultDatabasePath()
        => Path.Combine(AppContext.BaseDirectory, "data", "sistemax-local.db");

    private static string DefaultBackupDirectory()
        => Path.Combine(AppContext.BaseDirectory, "data", "backups");
}
