using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Fsm;

/// <summary>
/// FSM de <see cref="StatusAtivoDeCapital"/> — mesma disciplina de <see cref="StatusProjetoFsm"/>
/// (regra dura R4). <c>EmUso → Encerrado</c> é automática (última competência do cronograma
/// reconhecida pelo cron — ver <see cref="AtivoDeCapital.ReconhecerCompetencia"/>);
/// <c>EmUso|Encerrado → Baixado</c> é a baixa antecipada/write-off (§4.5/§4.6 dos designs).
/// <see cref="StatusAtivoDeCapital.Vendido"/> é reservado para a fatia I4 (Imobilizado) — nenhuma
/// transição para ele está habilitada aqui ainda.
/// </summary>
public static class StatusAtivoDeCapitalFsm
{
    private static readonly Dictionary<StatusAtivoDeCapital, StatusAtivoDeCapital[]> Transicoes = new()
    {
        [StatusAtivoDeCapital.EmUso] = [StatusAtivoDeCapital.Encerrado, StatusAtivoDeCapital.Baixado],
        [StatusAtivoDeCapital.Encerrado] = [StatusAtivoDeCapital.Baixado]
    };

    public static bool PodeTransitar(StatusAtivoDeCapital de, StatusAtivoDeCapital para)
        => Transicoes.TryGetValue(de, out var permitidos) && permitidos.Contains(para);

    public static Result AssertirTransicao(StatusAtivoDeCapital de, StatusAtivoDeCapital para)
        => PodeTransitar(de, para)
            ? Result.Ok()
            : Result.Falhar(new Error(
                "financeiro.ativo.transicao_invalida",
                $"Transição de status inválida: {de} → {para}."));
}
