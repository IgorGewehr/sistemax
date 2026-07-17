namespace SistemaX.Modules.Compras.Domain.Fornecedores;

/// <summary>
/// FSM de <see cref="Fornecedor"/>:
///
/// <code>
///   Ativo ⇄ Inativo
///   Ativo → Bloqueado → Ativo
/// </code>
///
/// <see cref="Bloqueado"/> não recebe PEDIDO novo (fase 2), mas uma nota já emitida por um
/// fornecedor bloqueado ainda pode ser recebida — bloquear é sobre compras futuras, não sobre
/// mercadoria que já está a caminho. Ver <see cref="Fornecedor.TransicoesPermitidas"/>.
/// </summary>
public enum StatusFornecedor
{
    Ativo,
    Inativo,
    Bloqueado
}
