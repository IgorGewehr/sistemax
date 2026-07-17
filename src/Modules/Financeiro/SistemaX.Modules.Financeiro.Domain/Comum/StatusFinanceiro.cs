namespace SistemaX.Modules.Financeiro.Domain.Comum;

/// <summary>
/// Status compartilhado por <c>Parcela</c> e pelo agregado <c>ContaAPagar</c>/<c>ContaAReceber</c>
/// (o status da conta é a agregação do status das suas parcelas). Ver
/// <see cref="Fsm.StatusFinanceiroFsm"/> para as transições permitidas.
/// </summary>
public enum StatusFinanceiro
{
    Aberto,
    Parcial,
    Pago,
    Atrasado,
    Cancelado
}
