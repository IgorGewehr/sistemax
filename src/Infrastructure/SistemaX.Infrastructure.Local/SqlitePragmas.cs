using Microsoft.Data.Sqlite;

namespace SistemaX.Infrastructure.Local;

/// <summary>
/// Aplica os pragmas de durabilidade/performance em toda conexão SQLite aberta pelo processo.
/// <c>journal_mode=WAL</c> persiste no cabeçalho do arquivo (setar uma vez basta), mas
/// <c>busy_timeout</c>/<c>foreign_keys</c>/<c>cache_size</c>/<c>temp_store</c>/<c>mmap_size</c>
/// são POR CONEXÃO — precisam ser reaplicados toda vez que uma nova <see cref="SqliteConnection"/>
/// é aberta, então este método deve rodar em TODA abertura, não só na primeira.
/// </summary>
public static class SqlitePragmas
{
    /// <summary>
    /// <c>synchronous=NORMAL</c> (não <c>FULL</c>) é escolha deliberada: combinado com WAL, o
    /// banco NUNCA corrompe mesmo em crash do processo — a pior perda possível é "as últimas
    /// transações committed que ainda não foram fsync'adas pro arquivo principal", nunca
    /// corrupção de fato. Só <c>synchronous=OFF</c> arriscaria corrupção real; não usamos.
    /// Ver docs/robustez/robustez-hardware-licoes.md §2.
    /// </summary>
    public static void Apply(SqliteConnection connection, LocalDatabaseOptions options)
    {
        using var cmd = connection.CreateCommand();

        cmd.CommandText = "PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA synchronous = NORMAL;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"PRAGMA busy_timeout = {(int)options.BusyTimeout.TotalMilliseconds};";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"PRAGMA cache_size = -{options.CacheSizeKb};";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA temp_store = MEMORY;";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"PRAGMA mmap_size = {options.MmapSizeBytes};";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"PRAGMA journal_size_limit = {options.JournalSizeLimitBytes};";
        cmd.ExecuteNonQuery();
    }
}
