using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Fsm;

/// <summary>
/// FSM de <see cref="StatusFinanceiro"/> — regra dura R4 do projeto: nenhum status muda sem
/// passar por aqui. <c>Pago</c> e <c>Cancelado</c> são terminais: corrigir um fato já pago
/// nunca é "voltar" o status, é lançar um estorno (novo fato imutável, ver
/// <c>docs/financeiro/financeiro-datamodel.md</c> §4.4).
/// </summary>
public static class StatusFinanceiroFsm
{
    private static readonly Dictionary<StatusFinanceiro, StatusFinanceiro[]> Transicoes = new()
    {
        [StatusFinanceiro.Aberto] =
        [
            StatusFinanceiro.Parcial, StatusFinanceiro.Pago, StatusFinanceiro.Atrasado, StatusFinanceiro.Cancelado
        ],
        [StatusFinanceiro.Parcial] = [StatusFinanceiro.Pago, StatusFinanceiro.Atrasado],
        [StatusFinanceiro.Atrasado] = [StatusFinanceiro.Parcial, StatusFinanceiro.Pago, StatusFinanceiro.Cancelado],
        [StatusFinanceiro.Pago] = [],
        [StatusFinanceiro.Cancelado] = []
    };

    public static bool PodeTransitar(StatusFinanceiro de, StatusFinanceiro para)
        => de == para || (Transicoes.TryGetValue(de, out var permitidos) && permitidos.Contains(para));

    public static Result AssertirTransicao(StatusFinanceiro de, StatusFinanceiro para)
        => PodeTransitar(de, para)
            ? Result.Ok()
            : Result.Falhar(new Error(
                "financeiro.fsm.transicao_invalida",
                $"Transição de status inválida: {de} → {para}."));
}
