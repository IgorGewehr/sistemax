namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// Natureza de uma linha dentro de <see cref="SessaoCaixa"/> — não confundir com
/// <see cref="TipoMovimentoFinanceiro"/> (o ledger de competência/caixa do <c>MovimentoFinanceiro</c>,
/// que exige <c>ParcelaId</c>). O ritual de caixa físico é mais pobre de propósito: só registra o
/// que mudou de mão na gaveta durante o turno, sem amarrar a uma parcela de contas a pagar/receber
/// (ver nota de design em <see cref="SessaoCaixa"/> sobre por que os dois ledgers não se fundem
/// ainda).
/// </summary>
public enum TipoMovimentoCaixa
{
    /// <summary>Dinheiro colocado na gaveta durante o turno (reforço de troco) — ENTRADA.</summary>
    Suprimento,

    /// <summary>Dinheiro retirado da gaveta durante o turno (para o cofre/banco) — SAÍDA. Nunca
    /// pode exceder o saldo esperado no momento do registro (ver <see cref="SessaoCaixa.RegistrarSangria"/>).</summary>
    Sangria,

    /// <summary>Venda paga em espécie registrada manualmente na sessão — ENTRADA. Hoje é lançada
    /// via <see cref="SessaoCaixa.RegistrarVendaEmEspecie"/> sem automação cross-módulo; a
    /// evolução natural (Vendas publica <c>VendaConcluida</c> com forma "dinheiro" → um handler do
    /// Financeiro chama este método se houver sessão aberta na conta-caixa) fica documentada aqui
    /// como PRÓXIMO PASSO, não implementada nesta revisão (seguindo R5 do CLAUDE.md: começa como
    /// capacidade do domínio, promove a evento de integração quando o segundo assinante aparecer —
    /// hoje só existiria um).</summary>
    VendaEmEspecie
}
