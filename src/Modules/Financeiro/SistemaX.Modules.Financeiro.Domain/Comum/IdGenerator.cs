namespace SistemaX.Modules.Financeiro.Domain.Comum;

/// <summary>
/// Toda entidade do Financeiro usa ULID como id (string) — ordenável por tempo de criação,
/// gerável no terminal/PDV sem coordenação com servidor central e sem colisão entre PDVs
/// (mesma lição de robustez de <c>docs/robustez/robustez-hardware-licoes.md</c> §3: numeração
/// não pode depender do servidor estar de pé).
/// </summary>
public static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
