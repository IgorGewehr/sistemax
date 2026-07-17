using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.Infrastructure.Hardware.Devices.CashDrawer;
using SistemaX.Infrastructure.Hardware.Devices.Printer;
using SistemaX.Infrastructure.Hardware.Devices.Scale;
using SistemaX.Infrastructure.Hardware.Devices.Scanner;
using SistemaX.Infrastructure.Hardware.Devices.Tef;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Manager;

/// <summary>
/// Orquestrador central de hardware. Regra do módulo inteiro (ver docs/robustez §5): erros de
/// hardware NUNCA são lançados ao fluxo de venda — todo método público aqui é um wrapper
/// "safeXxx" que devolve <see cref="Result"/>/<see cref="Result{T}"/>, mesmo que o adapter por
/// trás, por algum bug, acabe lançando (defesa em profundidade).
///
/// Também roda o health-check periódico com reconexão em backoff exponencial (5s, 10s, 20s, 40s,
/// 60s máx — configurável em <see cref="HardwareOptions.ReconnectBackoffSteps"/>), resetando o
/// contador de tentativas ao reconectar com sucesso, e permite trocar qualquer adapter em runtime
/// (ex.: operador troca de adquirente TEF nas Configurações) sem reiniciar o terminal.
/// </summary>
public sealed class HardwareManager : BackgroundService
{
    private readonly IOptions<HardwareOptions> _options;
    private readonly ILogger<HardwareManager> _logger;
    private readonly TefFallbackCoordinator _tefFallbackCoordinator;

    private readonly Dictionary<string, int> _consecutiveFailures = new();
    private readonly Dictionary<string, DateTimeOffset> _nextReconnectAttempt = new();

    private IPrinterAdapter _printer;
    private IScaleAdapter _scale;
    private ICashDrawerAdapter _cashDrawer;
    private IBarcodeScannerAdapter _scanner;
    private IReadOnlyList<ITefAdapter> _tefProvidersEmOrdem;

    public HardwareManager(
        IOptions<HardwareOptions> options,
        ILogger<HardwareManager> logger,
        TefFallbackCoordinator tefFallbackCoordinator,
        IPrinterAdapter? printer = null,
        IScaleAdapter? scale = null,
        ICashDrawerAdapter? cashDrawer = null,
        IBarcodeScannerAdapter? scanner = null,
        IReadOnlyList<ITefAdapter>? tefProvidersEmOrdem = null)
    {
        _options = options;
        _logger = logger;
        _tefFallbackCoordinator = tefFallbackCoordinator;

        // Null Object como valor inicial até o operador configurar hardware de verdade — nunca
        // trava o boot do terminal por falta de dispositivo físico conectado.
        _printer = printer ?? new NullPrinterAdapter();
        _scale = scale ?? new NullScaleAdapter();
        _cashDrawer = cashDrawer ?? new PrinterDrivenCashDrawerAdapter(_printer);
        _scanner = scanner ?? new NullBarcodeScannerAdapter();
        _tefProvidersEmOrdem = tefProvidersEmOrdem ?? [new NullTefAdapter()];
    }

    /// <summary>Levantado quando um dispositivo que estava com falha reconecta com sucesso — ex.: <see cref="PrintQueue.PrintQueueProcessor"/> drena a fila assim que a impressora volta.</summary>
    public event Action<string>? DeviceReconnected;

    public void SetPrinterAdapter(IPrinterAdapter adapter) => _printer = adapter;

    public void SetScaleAdapter(IScaleAdapter adapter) => _scale = adapter;

    public void SetCashDrawerAdapter(ICashDrawerAdapter adapter) => _cashDrawer = adapter;

    public void SetScannerAdapter(IBarcodeScannerAdapter adapter) => _scanner = adapter;

    /// <summary>Troca a cadeia de provedores de TEF em runtime (ex.: operador reconfigura o adquirente) sem reiniciar o terminal.</summary>
    public void SetTefProviders(IReadOnlyList<ITefAdapter> providersEmOrdem) => _tefProvidersEmOrdem = providersEmOrdem;

    public async Task<Result> SafePrintAsync(IReadOnlyList<PrintCommand> commands, CancellationToken ct = default)
    {
        try
        {
            return await _printer.PrintAsync(commands, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adapter de impressora lançou exceção inesperada — isto é um bug no adapter (deveria ter retornado Result.Falhar). Contendo aqui para não derrubar a venda.");
            return Result.Falhar(new Error("hardware.printer.excecao_inesperada", ex.Message));
        }
    }

    public async Task<Result<Reading>> SafeGetWeightAsync(CancellationToken ct = default)
    {
        try
        {
            return await _scale.GetWeightAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adapter de balança lançou exceção inesperada.");
            return Result.Falhar<Reading>(new Error("hardware.balanca.excecao_inesperada", ex.Message));
        }
    }

    public async Task<Result> SafeOpenDrawerAsync(CancellationToken ct = default)
    {
        try
        {
            return await _cashDrawer.OpenAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adapter de gaveta lançou exceção inesperada.");
            return Result.Falhar(new Error("hardware.gaveta.excecao_inesperada", ex.Message));
        }
    }

    /// <summary>
    /// Autorização de TEF SEMPRE por aqui — nunca chame um <see cref="ITefAdapter"/> diretamente.
    /// Delega para <see cref="TefFallbackCoordinator"/>, que encapsula a regra anti-cobrança-dupla.
    /// </summary>
    public async Task<TefAuthorizationResult> SafeAuthorizeTefAsync(TefTransactionRequest request, CancellationToken ct = default)
    {
        try
        {
            return await _tefFallbackCoordinator.AuthorizeAsync(_tefProvidersEmOrdem, request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "TefFallbackCoordinator lançou exceção inesperada — tratando como status indeterminado (nunca como negado/aprovado).");
            return new TefAuthorizationResult(TefAuthorizationOutcome.RequiresManualConfirmation, null, "Falha inesperada no coordenador de TEF — confirme manualmente antes de tentar novamente.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.Value.HealthCheckInterval);

        do
        {
            try
            {
                await RunHealthCheckCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ciclo de health-check de hardware falhou inesperadamente.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunHealthCheckCycleAsync(CancellationToken ct)
    {
        await CheckAndReconnectAsync("Printer", _printer.Health, () => _printer.ConnectAsync(ct), ct).ConfigureAwait(false);
        await CheckAndReconnectAsync("Scale", _scale.Health, () => _scale.ConnectAsync(ct), ct).ConfigureAwait(false);
        await CheckAndReconnectAsync("Scanner", _scanner.Health, () => _scanner.ConnectAsync(ct), ct).ConfigureAwait(false);

        for (var i = 0; i < _tefProvidersEmOrdem.Count; i++)
        {
            var provider = _tefProvidersEmOrdem[i];
            await CheckAndReconnectAsync($"Tef:{provider.Provider}", provider.Health, () => provider.ConnectAsync(ct), ct).ConfigureAwait(false);
        }
    }

    private async Task CheckAndReconnectAsync(string deviceKey, DeviceHealth currentHealth, Func<Task<Result>> connect, CancellationToken ct)
    {
        if (currentHealth.Status == DeviceStatus.Connected)
        {
            return;
        }

        if (_nextReconnectAttempt.TryGetValue(deviceKey, out var eligibleAt) && DateTimeOffset.UtcNow < eligibleAt)
        {
            return; // ainda dentro da janela de backoff — não tenta a cada tick pra não saturar porta serial/rede
        }

        var result = await connect().ConfigureAwait(false);
        var steps = _options.Value.ReconnectBackoffSteps;

        if (result.Sucesso)
        {
            // Reseta o contador de tentativas ao reconectar com sucesso — a lição do
            // Supermarket-OS (docs/robustez §5): nunca ficar preso num backoff crescente depois
            // de uma reconexão que já deu certo.
            _consecutiveFailures.Remove(deviceKey);
            _nextReconnectAttempt.Remove(deviceKey);
            _logger.LogInformation("Dispositivo '{Device}' (re)conectado com sucesso.", deviceKey);
            DeviceReconnected?.Invoke(deviceKey);
        }
        else
        {
            var failures = _consecutiveFailures.GetValueOrDefault(deviceKey, 0) + 1;
            _consecutiveFailures[deviceKey] = failures;

            var stepIndex = Math.Min(failures - 1, steps.Count - 1);
            var delay = steps.Count > 0 ? steps[stepIndex] : TimeSpan.FromSeconds(30);
            _nextReconnectAttempt[deviceKey] = DateTimeOffset.UtcNow.Add(delay);

            _logger.LogWarning("Dispositivo '{Device}' segue com falha (tentativa #{Tentativa}) — próxima reconexão em {Delay}.", deviceKey, failures, delay);
        }
    }
}
