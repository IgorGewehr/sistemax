namespace SistemaX.Modules.Financeiro.Domain.Comum;

/// <summary>
/// Referência ao fato de origem (módulo + id) que gerou um fato financeiro.
/// É A CHAVE DE IDEMPOTÊNCIA (docs/financeiro/financeiro-datamodel.md §4.3): antes de criar
/// uma <c>ContaAPagar</c>/<c>ContaAReceber</c>/<c>MovimentoFinanceiro</c>, o caso de uso consulta
/// o repositório por esta chave — se já existir, é replay do mesmo evento de integração,
/// não duplica o fato financeiro.
/// </summary>
public sealed record SourceRef(string Modulo, string Id)
{
    /// <summary>Chave estável e determinística usada para busca de idempotência.</summary>
    public string Chave => $"{Modulo}:{Id}";

    public override string ToString() => Chave;
}
