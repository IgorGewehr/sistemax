namespace SistemaX.Infrastructure.Hardware.Devices.Tef;

public enum TefTransactionStatus
{
    Approved,
    Denied,

    /// <summary>Ainda em processamento no adquirente — nunca tratar como definitivo.</summary>
    Pending,

    /// <summary>
    /// Não foi possível determinar o status (timeout na própria consulta, adquirente
    /// indisponível, etc.). Diferente de <see cref="Denied"/> — "desconhecido" NUNCA autoriza
    /// uma nova tentativa automática (ver <see cref="TefFallbackCoordinator"/>).
    /// </summary>
    Unknown
}

/// <param name="IdempotencyKey">
/// ULID único desta tentativa de cobrança. CONTRATO DURO para qualquer implementação real de
/// <see cref="ITefAdapter"/>: esta chave DEVE viajar no corpo da requisição ao adquirente (ex.:
/// campo <c>externalId</c>/<c>orderId</c> da API do PayGo/SiTef/Stone/Cappta) — nunca confiar
/// apenas em <see cref="SaleId"/>, que pode não ser suficiente se a mesma venda tentar autorizar
/// duas vezes (ex.: reenvio após timeout) e o adquirente não tratar esse campo como chave de
/// dedupe. Esta é a fraqueza #2 do Supermarket-OS corrigida (docs/robustez §5):
/// lá, o tipo carregava <c>idempotencyKey</c> mas o adapter PayGo nunca o enviava de fato.
/// </param>
public sealed record TefTransactionRequest(
    string SaleId,
    string IdempotencyKey,
    long ValorCentavos,
    string FormaPagamento);

public sealed record TefTransactionResult(
    TefTransactionStatus Status,
    string? Nsu,
    string? CodigoAutorizacao,
    string? Bandeira,
    string? MensagemAdquirente);

public sealed record TefStatusConsultaResult(TefTransactionStatus Status, string? Nsu, string? MensagemAdquirente);
