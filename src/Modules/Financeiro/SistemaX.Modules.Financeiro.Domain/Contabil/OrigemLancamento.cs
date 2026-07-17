namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// Rastro do fato de negócio que originou um <see cref="LancamentoContabil"/> — nunca um
/// lançamento contábil nasce "solto"; todo lançamento aponta para a <c>ContaAPagar</c>,
/// <c>ContaAReceber</c> ou <c>MovimentoFinanceiro</c> que o gerou.
/// </summary>
public sealed record OrigemLancamento(string Modulo, string TipoFato, string Id)
{
    public string Chave => $"{Modulo}.{TipoFato}:{Id}";

    public override string ToString() => Chave;
}
