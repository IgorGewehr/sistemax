namespace SistemaX.Infrastructure.Hardware.PrintQueue;

public enum PrintJobStatus
{
    Pending,
    Printing,
    Completed,
    Failed
}

/// <summary>
/// Um job de impressão persistido — o MESMO princípio de outbox durável usado para dados de
/// negócio (ver <c>SistemaX.Infrastructure.Local.Outbox</c>), reaplicado a hardware: cupom fiscal
/// não se perde só porque a impressora estava sem papel/offline no momento da venda
/// (docs/robustez §5).
/// </summary>
public sealed record PrintJob(
    string Id,
    string CommandsJson,
    PrintJobStatus Status,
    int Attempts,
    int MaxAttempts,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastAttemptAtUtc,
    string? LastError);
