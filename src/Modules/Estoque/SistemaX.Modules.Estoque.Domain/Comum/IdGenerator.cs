namespace SistemaX.Modules.Estoque.Domain.Comum;

/// <summary>
/// ULID como id (string) — ordenável por tempo de criação, gerável no terminal sem coordenação
/// com servidor central (mesmo padrão do Financeiro/Vendas).
/// </summary>
public static class IdGenerator
{
    public static string NovoId() => Ulid.NewUlid().ToString();
}
