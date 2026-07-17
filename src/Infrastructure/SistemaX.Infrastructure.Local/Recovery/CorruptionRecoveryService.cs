using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local.Backup;
using SistemaX.Infrastructure.Local.Kv;
using SistemaX.Infrastructure.Local.Migrations;

namespace SistemaX.Infrastructure.Local.Recovery;

/// <inheritdoc cref="ICorruptionRecoveryService"/>
public sealed class CorruptionRecoveryService(
    ILocalSqliteConnectionFactory connectionFactory,
    IBackupManager backupManager,
    IAppKeyValueStore kv,
    IOptions<LocalDatabaseOptions> options,
    ILogger<CorruptionRecoveryService> logger) : ICorruptionRecoveryService
{
    private const string LastFullCheckKey = "last_full_integrity_check_utc";

    public event EventHandler<CorruptionRecoveredEventArgs>? CorruptionRecovered;

    public async Task<bool> EnsureIntegrityOnBootAsync(CancellationToken ct = default)
    {
        var interval = options.Value.IntegrityCheckInterval;
        var lastRunRaw = await kv.GetAsync(LastFullCheckKey, ct).ConfigureAwait(false);

        if (lastRunRaw is not null
            && long.TryParse(lastRunRaw, out var lastRunMs)
            && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(lastRunMs) < interval)
        {
            logger.LogDebug("Integrity check completo pulado — última execução dentro do intervalo configurado ({Interval}).", interval);
            return false;
        }

        await kv.SetAsync(LastFullCheckKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(), ct).ConfigureAwait(false);

        var healthy = await RunCheckPragmaAsync("PRAGMA integrity_check;", ct).ConfigureAwait(false);
        if (healthy)
        {
            return false;
        }

        logger.LogCritical("PRAGMA integrity_check detectou corrupção no banco local — iniciando recuperação automática (fail-open).");
        await AttemptRecoveryAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RunQuickCheckAsync(CancellationToken ct = default)
    {
        var healthy = await RunCheckPragmaAsync("PRAGMA quick_check;", ct).ConfigureAwait(false);
        if (healthy)
        {
            return false;
        }

        logger.LogCritical("PRAGMA quick_check periódico detectou corrupção no banco local — iniciando recuperação automática (fail-open).");
        await AttemptRecoveryAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task AttemptRecoveryAsync(CancellationToken ct = default)
    {
        var dbPath = connectionFactory.DatabasePath;

        // Passo 1 — libera qualquer handle nativo mantido pelo pool de conexões do
        // Microsoft.Data.Sqlite; sem isso o rename abaixo falharia com arquivo em uso.
        SqliteConnection.ClearAllPools();

        // Passo 2 — preserva o arquivo corrompido para forense. NUNCA apaga.
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        var corruptedPath = $"{dbPath}.corrupted-{timestamp}";
        MoveIfExists(dbPath, corruptedPath);
        MoveIfExists(dbPath + "-wal", corruptedPath + "-wal");
        MoveIfExists(dbPath + "-shm", corruptedPath + "-shm");

        await LogRecoveryEventAsync("CorruptionDetected", $"Arquivo corrompido preservado em {corruptedPath}", ct).ConfigureAwait(false);

        // Passo 3 — tenta restaurar do backup mais recente.
        var latestBackup = backupManager.FindMostRecentBackup();
        string? restoredFrom = null;
        if (latestBackup is not null)
        {
            try
            {
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Copy(latestBackup, dbPath, overwrite: false);
                restoredFrom = latestBackup;
                logger.LogWarning("Banco local restaurado a partir do backup {Backup}.", latestBackup);
            }
            catch (IOException ex)
            {
                logger.LogError(ex, "Falha ao restaurar backup {Backup} — seguindo fail-open com banco novo/vazio.", latestBackup);
            }
        }

        // Passo 4 — fail-open: se não há backup (ou a restauração falhou), o próximo
        // OpenConnectionAsync abaixo simplesmente CRIA um arquivo novo/vazio (Mode=ReadWriteCreate)
        // — decisão consciente de manter o terminal operando mesmo sem histórico, em vez de
        // travar a loja esperando intervenção manual.
        if (restoredFrom is null)
        {
            logger.LogCritical("Nenhum backup disponível para restaurar — terminal seguirá com banco LOCAL NOVO (vazio). Histórico local anterior está preservado apenas no arquivo corrompido para forense.");
        }

        // Passo 5 — reabre (aplica pragmas) e garante que o schema de infraestrutura existe
        // (necessário no caso de banco novo/vazio do fail-open).
        await LocalSchemaMigrator.EnsureCreatedAsync(connectionFactory, ct).ConfigureAwait(false);

        await LogRecoveryEventAsync(
            "RecoveryCompleted",
            restoredFrom is not null ? $"Restaurado de {restoredFrom}" : "Fail-open: banco novo/vazio (sem backup disponível)",
            ct).ConfigureAwait(false);

        CorruptionRecovered?.Invoke(this, new CorruptionRecoveredEventArgs(corruptedPath, restoredFrom));
    }

    private async Task<bool> RunCheckPragmaAsync(string pragmaSql, CancellationToken ct)
    {
        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = pragmaSql;
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return string.Equals(result as string, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException ex)
        {
            logger.LogError(ex, "Exceção ao rodar {Pragma} — tratando como corrupção.", pragmaSql);
            return false;
        }
    }

    private async Task LogRecoveryEventAsync(string kind, string detail, CancellationToken ct)
    {
        try
        {
            await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO crash_recovery_events (occurred_at_utc, kind, detail) VALUES ($occurredAt, $kind, $detail);";
            cmd.Parameters.AddWithValue("$occurredAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$detail", detail);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            // A tabela de auditoria pode ainda não existir logo após um fail-open — não deixe a
            // auditoria derrubar o boot do terminal.
            logger.LogWarning(ex, "Não foi possível gravar evento de auditoria de recovery ({Kind}).", kind);
        }
    }

    private void MoveIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        try
        {
            File.Move(source, destination, overwrite: false);
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Falha ao mover {Origem} para {Destino} durante recuperação de corrupção.", source, destination);
        }
    }
}
