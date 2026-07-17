using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SistemaX.Infrastructure.Local.Backup;
using SistemaX.Infrastructure.Local.Identity;
using SistemaX.Infrastructure.Local.Kv;
using SistemaX.Infrastructure.Local.Ledger;
using SistemaX.Infrastructure.Local.Migrations;
using SistemaX.Infrastructure.Local.Outbox;
using SistemaX.Infrastructure.Local.Projections;
using SistemaX.Infrastructure.Local.Recovery;
using SistemaX.Infrastructure.Local.Sequences;
using SistemaX.Infrastructure.Local.UnitOfWork;
using SistemaX.Modules.Abstractions.Runtime;

namespace SistemaX.Infrastructure.Local.DependencyInjection;

/// <summary>Composition root deste projeto — um host chama isto uma vez e ganha toda a infraestrutura local.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSistemaXLocalInfrastructure(this IServiceCollection services, Action<LocalDatabaseOptions>? configure = null)
    {
        services.AddOptions<LocalDatabaseOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ILocalSqliteConnectionFactory, LocalSqliteConnectionFactory>();
        services.AddSingleton<IAppKeyValueStore, SqliteAppKeyValueStore>();
        services.AddSingleton<ITerminalIdentity, TerminalIdentityProvider>();

        services.AddSingleton<IOutboxStore, SqliteOutboxStore>();
        services.AddSingleton<ILocalSequenceAllocator, LocalSequenceAllocator>();
        services.AddSingleton<ILocalUnitOfWorkFactory, LocalUnitOfWorkFactory>();

        // Sessão ambiente (SCOPED — uma por caso de uso/requisição) que os repositórios SQLite
        // consultam para participar da transação em andamento. Ver ILocalSessao.
        services.AddScoped<ILocalSessao, LocalSessao>();

        services.AddSingleton<IBackupManager, BackupManager>();
        services.AddSingleton<ICorruptionRecoveryService, CorruptionRecoveryService>();
        services.AddSingleton<CrashRecoveryRunner>();

        // Migração v1 da infra (outbox/sequências/kv/crash-recovery) — os módulos de negócio
        // registram as suas via AddModuleSchemaMigration<T>() logo abaixo.
        services.AddSingleton<IModuleSchemaMigration, LocalInfraSchemaMigration>();

        // F0 do plano de inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md,
        // ADR-0005): ledger append-only de eventos de integração + motor de projeções. Migração
        // v2 do módulo "local" (integration_events + projection_state) — ver
        // IntegrationEventsSchemaMigration. Sempre SQLite, nunca gated pela config de
        // "persistencia" de cada módulo de negócio (mesmo racional do outbox): é a fonte de
        // verdade histórica, não um detalhe de adapter que o módulo escolhe trocar.
        services.AddSingleton<IIntegrationEventLedgerStore, SqliteIntegrationEventLedgerStore>();
        services.AddSingleton<IModuleSchemaMigration, IntegrationEventsSchemaMigration>();
        services.AddSingleton<IProjectionStateStore, SqliteProjectionStateStore>();
        services.AddSingleton<ProjectionRunner>();

        services.AddSingleton<SchemaMigrationRunner>();

        services.AddSingleton<LocalDatabaseBootstrapper>();

        // Registrado tanto como serviço concreto (para chamada manual em hosts sem Generic Host,
        // ex.: `await sp.GetRequiredService<LocalDatabaseBootstrapper>().BootstrapAsync();`)
        // quanto como IHostedService (para hosts com Generic Host, ex.: Store.Server).
        services.AddHostedService(sp => sp.GetRequiredService<LocalDatabaseBootstrapper>());
        services.AddHostedService<PeriodicIntegrityCheckService>();

        // Catch-up de projeções — precisa rodar DEPOIS do bootstrapper (schema aplicado) e é
        // seguro rodar mesmo sem nenhuma IProjection registrada ainda (enumerable vazio).
        services.AddHostedService<ProjectionCatchUpHostedService>();

        return services;
    }

    /// <summary>
    /// Registra um <see cref="ICrashRecoveryHook"/> de um módulo de negócio. Chamado pelo módulo
    /// (ex.: Vendas.Infrastructure), nunca por este projeto — ele não conhece hooks concretos.
    /// </summary>
    public static IServiceCollection AddCrashRecoveryHook<THook>(this IServiceCollection services)
        where THook : class, ICrashRecoveryHook
    {
        services.AddSingleton<ICrashRecoveryHook, THook>();
        return services;
    }

    /// <summary>
    /// Registra uma <see cref="IModuleSchemaMigration"/> de um módulo de negócio (ex.:
    /// Compras.Infrastructure, quando decide persistir um port em SQLite). Chamado pelo módulo,
    /// nunca por este projeto — o <see cref="SchemaMigrationRunner"/> as descobre todas via DI,
    /// sem conhecer nenhuma concreta.
    /// </summary>
    public static IServiceCollection AddModuleSchemaMigration<TMigration>(this IServiceCollection services)
        where TMigration : class, IModuleSchemaMigration
    {
        services.AddSingleton<IModuleSchemaMigration, TMigration>();
        return services;
    }
}
