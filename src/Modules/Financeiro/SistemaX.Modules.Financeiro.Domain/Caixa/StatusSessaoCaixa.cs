namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// FSM de <see cref="SessaoCaixa"/>. Dois estados, transição única e terminal:
///
/// <code>
///   Aberta ──Fechar()──► Fechada
/// </code>
///
/// Não existe caminho de volta (Fechada é terminal — reabrir é abrir uma NOVA sessão, nunca mutar
/// uma já fechada) nem atalho. Ver <c>SistemaX.Modules.Financeiro.Domain.Fsm.StatusSessaoCaixaFsm</c>.
/// </summary>
public enum StatusSessaoCaixa
{
    /// <summary>Gaveta aberta — aceita suprimento/sangria/venda em espécie. Só pode existir UMA
    /// sessão Aberta por <c>ContaCaixaId</c> ao mesmo tempo (invariante aplicada na Application,
    /// ver <c>AbrirSessaoCaixaUseCase</c> — não é checável dentro do próprio agregado porque
    /// depende de outras instâncias persistidas).</summary>
    Aberta,

    /// <summary>Encerrada com contagem física feita — <see cref="SessaoCaixa.SaldoInformado"/> e
    /// <see cref="SessaoCaixa.Diferenca"/> passam a existir. Terminal.</summary>
    Fechada
}
