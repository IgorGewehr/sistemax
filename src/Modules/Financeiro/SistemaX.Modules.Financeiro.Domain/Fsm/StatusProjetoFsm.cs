using SistemaX.Modules.Financeiro.Domain.Projetos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Fsm;

/// <summary>
/// FSM de <see cref="StatusProjeto"/> — mesma disciplina de <see cref="StatusFinanceiroFsm"/>/
/// <see cref="StatusSessaoCaixaFsm"/> (regra dura R4: nenhum status muda sem passar por aqui).
/// Vive num helper LOCAL ao módulo (não em <c>SistemaX.Modules.Abstractions.Fsm&lt;T&gt;</c>) pelo
/// mesmo motivo dos outros dois: o projeto Domain do Financeiro não referencia Modules.Abstractions.
/// Única transição bidirecional do módulo: <c>Ativo ⇄ Arquivado</c> (docs/financeiro/
/// design-analise-por-projeto.md §3.1) — arquivar não desvincula nada, só reativa/desativa a
/// visibilidade nas listas e selects de tagging.
/// </summary>
public static class StatusProjetoFsm
{
    private static readonly Dictionary<StatusProjeto, StatusProjeto[]> Transicoes = new()
    {
        [StatusProjeto.Ativo] = [StatusProjeto.Arquivado],
        [StatusProjeto.Arquivado] = [StatusProjeto.Ativo]
    };

    public static bool PodeTransitar(StatusProjeto de, StatusProjeto para)
        => Transicoes.TryGetValue(de, out var permitidos) && permitidos.Contains(para);

    public static Result AssertirTransicao(StatusProjeto de, StatusProjeto para)
        => PodeTransitar(de, para)
            ? Result.Ok()
            : Result.Falhar(new Error(
                "financeiro.projeto.transicao_invalida",
                $"Transição de status inválida: {de} → {para}."));
}
