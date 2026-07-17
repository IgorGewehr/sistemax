using SistemaX.SharedKernel;

namespace SistemaX.Verticals.Assistencia;

/// <summary>
/// Orçamento enviado ao cliente: peças previstas + mão de obra + validade. Sem versionamento —
/// reenviar (<see cref="OrdemDeServico.EnviarOrcamento"/> chamado de novo em
/// <see cref="StatusOrdemServico.AguardandoAprovacao"/>) SUBSTITUI o anterior; o histórico de
/// transições já registra que houve reenvio, versionar é feature de v2 se o cliente real pedir
/// (§3 do plano).
/// </summary>
public sealed record Orcamento(IReadOnlyList<PecaOrcada> Pecas, Money MaoDeObra, int ValidadeDias, DateTimeOffset EnviadoEm)
{
    public Money TotalPecas => Pecas.Aggregate(Money.Zero, static (acumulado, peca) => acumulado + peca.Subtotal);

    /// <summary>Nunca digitado — sempre derivado (§4.3 do plano).</summary>
    public Money Total => MaoDeObra + TotalPecas;

    public DateTimeOffset VenceEm => EnviadoEm.AddDays(ValidadeDias);
}
