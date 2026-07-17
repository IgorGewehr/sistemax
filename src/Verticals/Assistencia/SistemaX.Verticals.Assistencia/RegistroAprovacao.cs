namespace SistemaX.Verticals.Assistencia;

/// <summary>Registro do ATO de decisão do cliente sobre o orçamento — existe para aprovação E
/// reprovação (um único objeto, não dois), porque ambos compartilham exatamente os mesmos
/// metadados: quando, por que canal, quem registrou (§4.4 do plano).</summary>
public sealed record RegistroAprovacao(
    DecisaoOrcamento Decisao,
    CanalAprovacao Canal,
    string? RegistradoPorId,
    string? RegistradoPorNome,
    DateTimeOffset Em);
