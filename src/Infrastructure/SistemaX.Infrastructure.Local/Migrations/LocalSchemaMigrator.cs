namespace SistemaX.Infrastructure.Local.Migrations;

/// <summary>
/// DDL idempotente (<c>CREATE TABLE IF NOT EXISTS</c>) das tabelas de INFRAESTRUTURA que este
/// projeto é dono: outbox, sequências locais, key-value de configuração interna e o log de
/// eventos de crash-recovery. Tabelas de NEGÓCIO (vendas, produtos, ...) são migradas pelos
/// próprios módulos (<c>SistemaX.Modules.*.Infrastructure</c>), usando a mesma
/// <see cref="ILocalSqliteConnectionFactory"/> — este projeto nunca conhece o schema de negócio.
///
/// O caminho NORMAL de aplicar este DDL é <see cref="LocalInfraSchemaMigration"/> (versão 1 do
/// módulo "local"), rodada pelo <see cref="SchemaMigrationRunner"/> no boot — ver
/// <c>AddSistemaXLocalInfrastructure</c>. Este tipo estático continua existindo só para o caminho
/// de EMERGÊNCIA de <c>Recovery.CorruptionRecoveryService</c>: depois de um fail-open (arquivo
/// corrompido movido pra forense, banco novo/vazio criado no lugar), o schema mínimo precisa
/// existir IMEDIATAMENTE — antes mesmo do runner rodar de novo — para que o próprio log de
/// recuperação (<c>crash_recovery_events</c>) possa ser gravado.
/// </summary>
public static class LocalSchemaMigrator
{
    internal const string Ddl =
        """
        CREATE TABLE IF NOT EXISTS outbox_messages (
            id                  TEXT PRIMARY KEY,
            entity_type         TEXT NOT NULL,
            entity_id           TEXT NOT NULL,
            operation           TEXT NOT NULL,
            payload_json        TEXT NOT NULL,
            created_at_utc      INTEGER NOT NULL,
            status              TEXT NOT NULL,
            attempts            INTEGER NOT NULL DEFAULT 0,
            next_attempt_at_utc INTEGER NULL,
            last_error          TEXT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_outbox_messages_status_next_attempt
            ON outbox_messages (status, next_attempt_at_utc);

        CREATE TABLE IF NOT EXISTS local_sequences (
            name  TEXT PRIMARY KEY,
            value INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS app_kv (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS crash_recovery_events (
            id             INTEGER PRIMARY KEY AUTOINCREMENT,
            occurred_at_utc INTEGER NOT NULL,
            kind           TEXT NOT NULL,
            detail         TEXT NOT NULL
        );
        """;

    public static async Task EnsureCreatedAsync(ILocalSqliteConnectionFactory connectionFactory, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Ddl;

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
