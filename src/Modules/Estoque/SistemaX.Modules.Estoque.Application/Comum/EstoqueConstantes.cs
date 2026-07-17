namespace SistemaX.Modules.Estoque.Application.Comum;

/// <summary>MVP: depósito único implícito (plano §3, V1). Multi-depósito real é V5 — o campo já
/// existe em <c>MovimentoDeEstoque.DepositoId</c> hoje porque adicioná-lo depois custaria migração.</summary>
public static class EstoqueConstantes
{
    public const string DepositoPadrao = "principal";
    public const string OperadorSistema = "sistema";
    public const string OperadorSistemaNome = "Sistema";
}
