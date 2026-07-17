using SistemaX.SharedKernel;

namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Guarda de transição de estado reutilizável por qualquer agregado com <c>status</c> — regra
/// dura do projeto (CLAUDE.md): nenhuma entidade muda <c>Status</c> livremente, sempre passa por
/// um mapa explícito de transições permitidas.
///
/// Não é um motor de state machine genérico com ações/efeitos — de propósito. É só a checagem
/// "de → para é permitido?", porque a lógica de CADA transição (o que mais muda, que evento
/// levanta) é específica do agregado e fica no próprio método do domínio (ex.:
/// <c>Venda.Concluir()</c>, <c>OrdemDeServico.Faturar()</c>). Ver
/// docs/arquitetura/COMO-CRIAR-UM-MODULO.md para o passo a passo de como modelar uma FSM nova.
///
/// Retorna <see cref="Result"/> em vez de lançar exceção: uma transição inválida é uma regra de
/// negócio ESPERADA (o operador tentou faturar uma OS ainda em orçamento), não um bug — mesma
/// filosofia do <see cref="Result"/> em SharedKernel.
/// </summary>
public static class Fsm<TStatus> where TStatus : struct, Enum
{
    public static Result ValidarTransicao(
        TStatus de,
        TStatus para,
        IReadOnlyDictionary<TStatus, TStatus[]> transicoesPermitidas)
    {
        if (transicoesPermitidas.TryGetValue(de, out var destinosPermitidos) &&
            destinosPermitidos.Contains(para))
        {
            return Result.Ok();
        }

        return Result.Falhar(new Error(
            "fsm.transicao_invalida",
            $"Transição de status inválida para {typeof(TStatus).Name}: '{de}' → '{para}'."));
    }
}
