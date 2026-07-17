using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Local.Backup;

namespace SistemaX.Infrastructure.Local.Migrations;

/// <summary>
/// Aplica, uma vez no boot, as migrações de TODOS os módulos registrados (ver
/// <see cref="IModuleSchemaMigration"/>). Cria a tabela <c>schema_migrations</c> (módulo, versão,
/// quando, checksum), calcula o conjunto pendente por módulo, tira um BACKUP antes de aplicar
/// qualquer coisa (via <see cref="IBackupManager"/> — nunca migrar sem rede de segurança) e
/// recusa DOWNGRADE (versão persistida maior que a maior declarada no código atual).
///
/// ORDEM: preserva a ordem de injeção de <see cref="IModuleSchemaMigration"/> — que é a ordem
/// TOPOLÓGICA em que <c>ModuleRegistry</c> registrou os módulos (dependido antes de dependente) —
/// e, dentro de um mesmo módulo, ordena por <see cref="IModuleSchemaMigration.Versao"/> crescente.
/// Cada migração pendente é aplicada em sua PRÓPRIA transação (migração + registro em
/// <c>schema_migrations</c> são atômicos juntos; uma migração que falha não impede o diagnóstico
/// de quais outras já tinham sido aplicadas antes dela).
/// </summary>
public sealed class SchemaMigrationRunner(
    ILocalSqliteConnectionFactory connectionFactory,
    IEnumerable<IModuleSchemaMigration> migracoes,
    IBackupManager backupManager,
    ILogger<SchemaMigrationRunner> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(ct).ConfigureAwait(false);
        await EnsureMigrationsTableAsync(connection, ct).ConfigureAwait(false);

        var aplicadas = await CarregarAplicadasAsync(connection, ct).ConfigureAwait(false);
        var pendentes = CalcularPendentes(aplicadas);

        if (pendentes.Count == 0)
        {
            logger.LogInformation("Schema local em dia — nenhuma migração pendente.");
            return;
        }

        logger.LogInformation("{Quantidade} migração(ões) de schema pendente(s) — criando backup pré-migração.", pendentes.Count);
        var backup = await backupManager.CreateBackupAsync(ct).ConfigureAwait(false);
        if (!backup.Sucesso)
        {
            // Fail-open deliberado (ver IBackupManager): um terminal recém-instalado normalmente
            // não tem dado nenhum a proteger ainda. Recusar o BOOT por causa disso travaria a
            // primeira instalação. O log crítico já foi emitido pelo próprio BackupManager.
            logger.LogWarning(
                "Backup pré-migração não foi criado ({Motivo}) — prosseguindo mesmo assim.",
                backup.MotivoFalha);
        }

        foreach (var migracao in pendentes)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await migracao.AplicarAsync(connection, transaction, ct).ConfigureAwait(false);
                await RegistrarAplicadaAsync(connection, transaction, migracao, ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                logger.LogInformation("Migração aplicada: {Modulo} v{Versao}.", migracao.Modulo, migracao.Versao);
            }
            catch
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }
    }

    private List<IModuleSchemaMigration> CalcularPendentes(IReadOnlyDictionary<string, int> aplicadas)
    {
        var pendentes = new List<IModuleSchemaMigration>();

        foreach (var grupo in migracoes.GroupBy(m => m.Modulo))
        {
            var ordenadas = grupo.OrderBy(m => m.Versao).ToList();
            var maiorDeclarada = ordenadas[^1].Versao;
            var maiorAplicada = aplicadas.GetValueOrDefault(grupo.Key, 0);

            if (maiorAplicada > maiorDeclarada)
            {
                throw new InvalidOperationException(
                    $"Módulo '{grupo.Key}' tem schema na versão {maiorAplicada} persistida em disco, mas o " +
                    $"código só declara até a versão {maiorDeclarada} — isso é um DOWNGRADE (binário mais " +
                    "antigo rodando sobre um banco mais novo) e não é suportado. Auto-update nunca deve " +
                    "regredir a versão instalada sem antes restaurar o backup pré-migração.");
            }

            pendentes.AddRange(ordenadas.Where(m => m.Versao > maiorAplicada));
        }

        return pendentes;
    }

    private static async Task EnsureMigrationsTableAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                modulo      TEXT NOT NULL,
                versao      INTEGER NOT NULL,
                aplicada_em TEXT NOT NULL,
                checksum    TEXT NOT NULL,
                PRIMARY KEY (modulo, versao)
            );
            """;

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, int>> CarregarAplicadasAsync(SqliteConnection connection, CancellationToken ct)
    {
        var resultado = new Dictionary<string, int>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT modulo, MAX(versao) FROM schema_migrations GROUP BY modulo;";

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            resultado[reader.GetString(0)] = reader.GetInt32(1);
        }

        return resultado;
    }

    private static async Task RegistrarAplicadaAsync(
        SqliteConnection connection, SqliteTransaction transaction, IModuleSchemaMigration migracao, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO schema_migrations (modulo, versao, aplicada_em, checksum)
            VALUES ($modulo, $versao, $aplicadaEm, $checksum);
            """;
        cmd.Parameters.AddWithValue("$modulo", migracao.Modulo);
        cmd.Parameters.AddWithValue("$versao", migracao.Versao);
        cmd.Parameters.AddWithValue("$aplicadaEm", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$checksum", migracao.Checksum);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
