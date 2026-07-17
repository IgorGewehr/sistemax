namespace SistemaX.Infrastructure.Sync.Conflict;

/// <summary>
/// Política explícita POR ENTIDADE — nunca um único "last write wins" genérico para tudo (ver
/// docs/robustez/robustez-hardware-licoes.md §3). A escolha errada aqui é como se perde dinheiro
/// (sobrescrever uma venda) ou se diverge cadastro entre terminais.
/// </summary>
public enum ConflictStrategy
{
    /// <summary>
    /// Dado financeiro/transacional (venda, pagamento, sessão de caixa, movimento de caixa,
    /// movimento de estoque como LANÇAMENTO imutável): o terminal que originou SEMPRE vence.
    /// Nunca descarte dinheiro real por causa de um conflito de sincronização.
    /// </summary>
    TerminalWins,

    /// <summary>
    /// Cadastro (produto, categoria, cliente, configuração): o servidor é autoridade — preço e
    /// cadastro não devem divergir por terminal. Mas se o terminal carregar uma <c>version</c>
    /// MAIOR que a do servidor (ex.: um cadastro editado direto num terminal offline há mais
    /// tempo que o último sync do servidor), aceita o terminal — ver <see cref="ConflictMath.ResolveByVersion"/>.
    /// </summary>
    ServerWinsWithVersion,

    /// <summary>
    /// Contador agregado (saldo de estoque): nunca substitui o valor absoluto — soma as
    /// VARIAÇÕES (delta). Resolve o clássico bug de "dois terminais decrementam estoque ao mesmo
    /// tempo, servidor aplica só o último e perde uma das duas vendas". Ver
    /// <see cref="ConflictMath.ReconcileByDelta"/>.
    /// </summary>
    ReconcileDelta
}
