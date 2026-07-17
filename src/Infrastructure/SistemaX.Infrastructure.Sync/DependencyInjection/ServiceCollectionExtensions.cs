using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Sync.Adapters;
using SistemaX.Infrastructure.Sync.ChangeLog;
using SistemaX.Infrastructure.Sync.Client;
using SistemaX.Infrastructure.Sync.Conflict;
using SistemaX.Infrastructure.Sync.Idempotency;
using SistemaX.Infrastructure.Sync.Realtime;
using SistemaX.Infrastructure.Sync.Server;

namespace SistemaX.Infrastructure.Sync.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra o lado CLIENTE do motor de sync para UM salto — o mesmo motor serve os 2 saltos
    /// da topologia, só muda esta configuração (endereço upstream + nome do salto). Chame uma vez
    /// por salto que este processo EMPURRA: <c>Host.Desktop</c> aponta pro servidor de loja;
    /// <c>Store.Server</c> chama de novo (outra named options) apontando pra nuvem.
    /// </summary>
    public static IServiceCollection AddSistemaXSyncClient(this IServiceCollection services, Action<SyncOptions> configure)
    {
        services.AddOptions<SyncOptions>().Configure(configure);

        services.AddHttpClient<ISyncTransportAdapter, HttpSyncTransportAdapter>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SyncOptions>>().Value;
            client.BaseAddress = opts.UpstreamBaseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<ISyncStorageAdapter, Adapters.LocalOutboxStorageAdapter>();
        services.TryAddSingleton<IConflictResolutionPolicy, DefaultConflictResolutionPolicy>();
        services.TryAddSingleton<ConflictResolver>();

        services.AddSingleton<SyncEngine>();
        services.AddHostedService(sp => sp.GetRequiredService<SyncEngine>());
        services.AddHostedService<SyncWebSocketClient>();

        return services;
    }

    /// <summary>
    /// Registra o lado RECEPTOR de um salto (idempotência + changelog + resolução de conflito).
    /// Um host com endpoints HTTP (<c>Store.Server</c>, <c>Cloud.Api</c>) injeta
    /// <see cref="SyncInboundService"/> nas suas rotas de <c>/api/sync/batch</c> e
    /// <c>/api/sync/pull</c>. Pode conviver no MESMO processo que <see cref="AddSistemaXSyncClient"/>
    /// — é exatamente o caso do servidor de loja, que recebe de PDVs e empurra para a nuvem.
    /// </summary>
    public static IServiceCollection AddSistemaXSyncInbound(this IServiceCollection services)
    {
        services.AddSingleton<IProcessedMessageStore, SqliteProcessedMessageStore>();
        services.AddSingleton<IChangeLogStore, SqliteChangeLogStore>();
        services.TryAddSingleton<IConflictResolutionPolicy, DefaultConflictResolutionPolicy>();
        services.TryAddSingleton<ConflictResolver>();
        services.AddSingleton<SyncInboundService>();

        return services;
    }

    /// <summary>Registra um applier de mudança remota para um tipo de entidade (implementado pelo módulo dono).</summary>
    public static IServiceCollection AddRemoteChangeApplier<TApplier>(this IServiceCollection services)
        where TApplier : class, IRemoteChangeApplier
    {
        services.AddSingleton<IRemoteChangeApplier, TApplier>();
        return services;
    }
}
