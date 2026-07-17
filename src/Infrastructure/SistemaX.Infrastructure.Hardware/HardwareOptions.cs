namespace SistemaX.Infrastructure.Hardware;

/// <summary>Configuração do módulo de hardware do terminal — health-check, backoff de reconexão e a fila de impressão persistida.</summary>
public sealed class HardwareOptions
{
    public const string SectionName = "SistemaX:Hardware";

    /// <summary>Intervalo entre checagens de saúde de todos os dispositivos configurados.</summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Degraus de backoff de reconexão (mesma progressão do Supermarket-OS: 5s, 10s, 20s, 40s,
    /// 60s máx) — nunca tenta a cada 1s (satura porta serial/rede), nunca desiste de vez.
    /// </summary>
    public IReadOnlyList<TimeSpan> ReconnectBackoffSteps { get; set; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60)
    ];

    /// <summary>Caminho do arquivo SQLite da fila de impressão. Independente do banco local de negócio (ver README — decisão de desacoplamento).</summary>
    public string PrintQueueDatabasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "data", "hardware-print-queue.db");

    /// <summary>Tentativas antes de um job de impressão ser marcado como definitivamente falho (visível para retry manual).</summary>
    public int PrintJobMaxAttempts { get; set; } = 5;

    /// <summary>Intervalo do auto-retry da fila de impressão quando a impressora está saudável.</summary>
    public TimeSpan PrintQueueRetryInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Timeout por tentativa de autorização TEF antes de consultar o status da transação original (nunca reenviar cegamente).</summary>
    public TimeSpan TefAuthorizationTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Quantas vezes consultar o status da transação original antes de exigir confirmação manual do operador.</summary>
    public int TefStatusPollMaxAttempts { get; set; } = 3;

    public TimeSpan TefStatusPollInterval { get; set; } = TimeSpan.FromSeconds(3);
}
