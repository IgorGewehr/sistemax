namespace SistemaX.Modules.Identidade.Domain.Comum;

/// <summary>
/// ULID como id (string) — ordenável por tempo de criação, gerável no terminal sem coordenação
/// com servidor central (mesmo padrão do Financeiro/Vendas/Estoque/Compras).
/// </summary>
public static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
