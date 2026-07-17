namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>Sentido do <c>MovimentoFinanceiro</c> — dinheiro entrando ou saindo de uma conta/caixa.</summary>
public enum TipoMovimentoFinanceiro
{
    Entrada,
    Saida
}

/// <summary>Onde a <c>ContaBancariaCaixa</c> fisicamente existe.</summary>
public enum TipoContaBancariaCaixa
{
    ContaCorrente,
    CaixaFisico,
    CarteiraDigital
}
