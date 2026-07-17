namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>Lado da partida contábil dentro de um <c>LancamentoContabil</c>.</summary>
public enum NaturezaPartida
{
    Debito,
    Credito
}

/// <summary>Natureza da conta no plano de contas simplificado (docs/financeiro-datamodel.md §2.3).</summary>
public enum TipoContaContabil
{
    Ativo,
    Passivo,
    Receita,
    Despesa
}
