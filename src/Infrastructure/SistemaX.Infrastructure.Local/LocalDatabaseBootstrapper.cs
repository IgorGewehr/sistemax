using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SistemaX.Infrastructure.Local.Migrations;
using SistemaX.Infrastructure.Local.Recovery;

namespace SistemaX.Infrastructure.Local;

/// <summary>
/// O gancho de crash-recovery no BOOT. Sequência, nesta ordem:
/// 1. Aplica as migrações de schema pendentes — infra (módulo "local") + todas as de negócio
///    registradas pelos módulos habilitados nesta instalação (ver <see cref="SchemaMigrationRunner"/>).
/// 2. Roda o <c>integrity_check</c> completo se estiver na hora (recupera sozinho se corrompido).
/// 3. Executa todos os <see cref="ICrashRecoveryHook"/> registrados pelos módulos de negócio.
///
/// Registrado como <see cref="IHostedService"/> para hosts que usam o Generic Host
/// (<c>SistemaX.Store.Server</c>); para hosts que não usam Generic Host (ex.: um app desktop
/// simples), chame <see cref="BootstrapAsync"/> diretamente no início do <c>Program.cs</c> — é o
/// mesmo método, só muda quem o invoca.
/// </summary>
public sealed class LocalDatabaseBootstrapper(
    ILocalSqliteConnectionFactory connectionFactory,
    SchemaMigrationRunner schemaMigrationRunner,
    ICorruptionRecoveryService corruptionRecoveryService,
    CrashRecoveryRunner crashRecoveryRunner,
    ILogger<LocalDatabaseBootstrapper> logger) : IHostedService
{
    public async Task BootstrapAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Inicializando banco local em {Caminho}.", connectionFactory.DatabasePath);

        await schemaMigrationRunner.RunAsync(ct).ConfigureAwait(false);
        await corruptionRecoveryService.EnsureIntegrityOnBootAsync(ct).ConfigureAwait(false);
        await crashRecoveryRunner.RunAllAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Banco local pronto.");
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken) => BootstrapAsync(cancellationToken);

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
