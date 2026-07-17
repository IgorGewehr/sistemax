namespace SistemaX.Verticals.Assistencia;

/// <summary>Canal pelo qual o cliente comunicou a decisão sobre o orçamento. 3 opções fixas
/// (não um texto livre) — a UI mostra 3 botões, não um dropdown (§4.4 do plano).</summary>
public enum CanalAprovacao
{
    Presencial,
    WhatsApp,
    Telefone
}
