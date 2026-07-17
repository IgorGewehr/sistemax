using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Hardware.Devices.CashDrawer;
using SistemaX.Infrastructure.Hardware.Devices.Printer;
using SistemaX.Infrastructure.Hardware.Devices.Scale;
using SistemaX.Infrastructure.Hardware.Devices.Scanner;
using SistemaX.Infrastructure.Hardware.Devices.Tef;
using SistemaX.Infrastructure.Hardware.Manager;
using SistemaX.Infrastructure.Hardware.PrintQueue;

namespace SistemaX.Infrastructure.Hardware.DependencyInjection;

/// <summary>
/// Composition root deste projeto. Por padrão registra TODOS os Null Objects (terminal sobe sem
/// nenhum hardware físico configurado) — o host chama <c>SetPrinterAdapter</c>/<c>SetScaleAdapter</c>/
/// etc. no <see cref="HardwareManager"/> depois que o operador configura hardware de verdade nas
/// Configurações (ou lê a configuração persistida no boot e já chama isso antes de subir).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSistemaXHardwareInfrastructure(this IServiceCollection services, Action<HardwareOptions>? configure = null)
    {
        services.AddOptions<HardwareOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<TefProviderFactory>();
        services.AddSingleton<TefFallbackCoordinator>();

        services.AddSingleton<IPrintQueueStore, SqlitePrintQueueStore>();

        services.AddSingleton<HardwareManager>();
        services.AddHostedService(sp => sp.GetRequiredService<HardwareManager>());
        services.AddHostedService<PrintQueueProcessor>();

        return services;
    }
}
