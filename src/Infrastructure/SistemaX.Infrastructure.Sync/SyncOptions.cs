namespace SistemaX.Infrastructure.Sync;

/// <summary>
/// Configuração de UM salto do motor de sync de 3 camadas (PDV ↔ ServidorDeLoja ↔ Nuvem). O
/// MESMO <see cref="Client.SyncEngine"/> serve os 2 saltos — o que muda é só esta configuração:
/// <list type="bullet">
/// <item>Salto 1 (PDV → ServidorDeLoja): <c>Host.Desktop</c> registra com
/// <see cref="UpstreamBaseAddress"/> apontando para o servidor de loja na LAN.</item>
/// <item>Salto 2 (ServidorDeLoja → Nuvem): <c>Store.Server</c> registra OUTRA instância destas
/// options apontando para a API da nuvem — ele é cliente do salto 2 e, ao mesmo tempo, receptor
/// do salto 1 (ver <c>SistemaX.Infrastructure.Sync.Server.SyncInboundService</c>).</item>
/// </list>
/// </summary>
public sealed class SyncOptions
{
    public const string SectionName = "SistemaX:Sync";

    /// <summary>Nome do salto — só para logs/telemetria (ex.: "PdvParaLoja", "LojaParaNuvem").</summary>
    public string HopName { get; set; } = "PdvParaLoja";

    /// <summary>Identifica de forma estável, nos logs e no wire, quem está falando (normalmente o TerminalId).</summary>
    public Uri UpstreamBaseAddress { get; set; } = new("http://localhost:5080");

    public string PushPath { get; set; } = "/api/sync/batch";
    public string PullPath { get; set; } = "/api/sync/pull";
    public string PingPath { get; set; } = "/api/sync/ping";
    public string WebSocketPath { get; set; } = "/ws/sync";

    /// <summary>Tamanho máximo de lote por push — mesma ordem de grandeza do Supermarket-OS (50).</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Intervalo do ciclo push+pull quando não há WS ativo empurrando flush sob demanda.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Tentativas antes de mover para dead-letter (visível/retry manual — nunca "muda" silenciosamente).</summary>
    public int MaxRetries { get; set; } = 10;

    public TimeSpan BackoffBase { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan BackoffMax { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Acima deste número de mensagens pendentes no outbox, loga ALERTA (nunca bloqueia venda) —
    /// corrige a fraqueza do Supermarket-OS de fila sem sinalização proativa (ver docs/robustez §3).
    /// </summary>
    public int PendingQueueAlertThreshold { get; set; } = 5000;

    public int MaxPullItemsPerRequest { get; set; } = 500;
}
