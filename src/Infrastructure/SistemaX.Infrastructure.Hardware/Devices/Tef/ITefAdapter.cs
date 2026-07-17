using SistemaX.Infrastructure.Hardware.Common;
using SistemaX.SharedKernel;

namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

/// <summary>
/// Abstração de adquirente de pagamento eletrônico. O resto do app só conhece esta interface —
/// trocar de adquirente (PayGo/SiTef/Stone/Cappta/ConnectTef) é configuração via
/// <see cref="TefProviderFactory"/>, nunca código novo em código de venda (ver docs/robustez §5).
/// </summary>
public interface ITefAdapter
{
    /// <summary>Nome estável do provedor (ex.: "paygo", "sitef", "stone", "cappta", "connecttef", "mock").</summary>
    string Provider { get; }

    DeviceHealth Health { get; }

    Task<Result> ConnectAsync(CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Inicia uma autorização. IMPLEMENTAÇÕES REAIS DEVEM enviar <see cref="TefTransactionRequest.IdempotencyKey"/>
    /// no corpo da requisição ao adquirente (ver contrato em <see cref="TefTransactionRequest"/>).
    /// NUNCA chame isto uma segunda vez para a mesma venda sem antes checar
    /// <see cref="GetTransactionStatusAsync"/> — use <see cref="TefFallbackCoordinator"/>, que
    /// encapsula essa regra; não chame adapters de TEF diretamente do fluxo de venda.
    /// </summary>
    Task<Result<TefTransactionResult>> StartTransactionAsync(TefTransactionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Consulta, pela MESMA <paramref name="idempotencyKey"/>, o status real da transação no
    /// adquirente. É a peça central da regra anti-cobrança-dupla: um timeout local NUNCA
    /// significa que o adquirente não processou — só esta consulta sabe a verdade.
    /// </summary>
    Task<Result<TefStatusConsultaResult>> GetTransactionStatusAsync(string idempotencyKey, CancellationToken ct = default);
}
