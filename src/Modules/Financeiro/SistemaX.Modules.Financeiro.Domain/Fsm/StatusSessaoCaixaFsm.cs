using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Fsm;

/// <summary>
/// FSM de <see cref="Caixa.StatusSessaoCaixa"/> — mesma disciplina de <see cref="StatusFinanceiroFsm"/>
/// (regra dura R4: nenhum status muda sem passar por aqui). Só uma transição existe e é terminal:
/// <c>Aberta → Fechada</c>. Vive num helper LOCAL ao módulo (não em
/// <c>SistemaX.Modules.Abstractions.Fsm&lt;T&gt;</c>) pelo mesmo motivo de <c>StatusFinanceiroFsm</c>:
/// o projeto Domain do Financeiro não referencia Modules.Abstractions.
/// </summary>
public static class StatusSessaoCaixaFsm
{
    private static readonly Dictionary<Caixa.StatusSessaoCaixa, Caixa.StatusSessaoCaixa[]> Transicoes = new()
    {
        [Caixa.StatusSessaoCaixa.Aberta] = [Caixa.StatusSessaoCaixa.Fechada],
        [Caixa.StatusSessaoCaixa.Fechada] = []
    };

    public static bool PodeTransitar(Caixa.StatusSessaoCaixa de, Caixa.StatusSessaoCaixa para)
        => Transicoes.TryGetValue(de, out var permitidos) && permitidos.Contains(para);

    public static Result AssertirTransicao(Caixa.StatusSessaoCaixa de, Caixa.StatusSessaoCaixa para)
        => PodeTransitar(de, para)
            ? Result.Ok()
            : Result.Falhar(new Error(
                "financeiro.sessao_caixa.transicao_invalida",
                $"Transição de status inválida: {de} → {para}."));
}
