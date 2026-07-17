namespace SistemaX.Modules.Estoque.Domain.Comum;

/// <summary>
/// Referência ao fato de origem (módulo + id) que gerou um <c>MovimentoDeEstoque</c> — mesma forma
/// e mesmo papel do <c>SourceRef</c> do Financeiro (cada módulo tem a sua cópia de propósito:
/// nenhum módulo referencia o Domain de outro). É a base da <c>ChaveIdempotencia</c>: reprocessar
/// o mesmo evento de integração não pode duplicar movimento no razão.
/// </summary>
public sealed record SourceRef(string Modulo, string Id)
{
    public string Chave => $"{Modulo}:{Id}";

    public override string ToString() => Chave;
}
