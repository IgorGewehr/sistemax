namespace SistemaX.Modules.Vendas.Domain;

/// <summary>
/// FSM de <see cref="Venda"/>. Três estados, transições estritamente lineares:
///
/// <code>
///   Aberta ──Concluir()──► Concluida ──Estornar()──► Estornada
/// </code>
///
/// Não existe caminho de volta (Estornada é terminal) nem atalho (Aberta não vai direto para
/// Estornada — precisa ter sido concluída primeiro). Ver <see cref="Venda.TransicoesPermitidas"/>.
/// </summary>
public enum StatusVenda
{
    /// <summary>Carrinho em montagem — itens podem ser adicionados/removidos.</summary>
    Aberta,

    /// <summary>Fechada com forma de pagamento definida. Levanta o evento de domínio que vira
    /// <c>VendaConcluida</c> (evento de integração) para o Financeiro.</summary>
    Concluida,

    /// <summary>Estorno de uma venda concluída. Levanta o evento que vira <c>VendaEstornada</c>.
    /// Terminal — uma venda estornada nunca volta a Concluida.</summary>
    Estornada
}
