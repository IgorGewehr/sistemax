namespace SistemaX.Verticals.Assistencia;

/// <summary>Uma linha por transição de <see cref="StatusOrdemServico"/> — substitui N campos de
/// data espalhados pelo agregado (dataDiagnostico, dataAprovacao, ...) por uma única lista
/// append-only, de onde <c>diasNaEtapa</c> e a linha do tempo da UI (§8.4 do plano) derivam.</summary>
public sealed record HistoricoTransicaoOs(
    StatusOrdemServico De,
    StatusOrdemServico Para,
    DateTimeOffset Em,
    string? PorId = null,
    string? PorNome = null);
