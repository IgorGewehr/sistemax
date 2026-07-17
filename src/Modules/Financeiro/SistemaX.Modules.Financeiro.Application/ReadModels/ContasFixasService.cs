using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um template de recorrência ativo — a linha da tabela "Todas as recorrências" da lente
/// Contas fixas (docs/wiring/financeiro-telas-restantes.md §2/§C). SIMPLIFICAÇÃO DO MVP (mesmo
/// espírito do XML doc de <see cref="RecorrenciaAgg"/>): é o TEMPLATE (valor previsto, dia fixo,
/// frequência) + a próxima ocorrência projetada, não o histórico mensal REALIZADO cruzando
/// <c>ContaAPagar</c>/<c>ContaAReceber</c> por <c>SourceRef</c> — esse cruzamento (variação vs
/// média de 6 meses, "emAlerta") é um read-model maior, fora do escopo pedido aqui.
/// <see cref="ProximaOcorrencia"/> vem <c>null</c> quando a recorrência já passou da
/// <c>DataFim</c> (nada de inventar uma data além do que o template permite).</summary>
public sealed record ContaFixaResumo(
    string Id, string Descricao, string CategoriaId, Money ValorPrevisto, int? DiaFixo,
    string Frequencia, string Tipo, DateTimeOffset? ProximaOcorrencia);

/// <summary>
/// Painel "Contas fixas" (a lente de despesas/receitas recorrentes de Recorrentes) — expõe o
/// domínio de <see cref="RecorrenciaAgg"/> já existente (<see cref="IRecorrenciaRepository"/>),
/// sem inventar nenhum agregado novo.
/// </summary>
public sealed class ContasFixasService(IRecorrenciaRepository recorrencias)
{
    public async Task<IReadOnlyList<ContaFixaResumo>> ListarAsync(string businessId, CancellationToken ct = default)
    {
        var ativas = await recorrencias.ListarAtivasAsync(businessId, ct).ConfigureAwait(false);

        return ativas
            .Select(r => new ContaFixaResumo(
                r.Id, r.Descricao, r.CategoriaId, r.ValorPrevisto, r.DiaFixo,
                r.Frequencia.ToString(), r.Tipo.ToString(), ProximaOcorrenciaOuNulo(r)))
            .OrderBy(r => r.ProximaOcorrencia ?? DateTimeOffset.MaxValue)
            .ToList();
    }

    private static DateTimeOffset? ProximaOcorrenciaOuNulo(RecorrenciaAgg recorrencia)
    {
        var resultado = recorrencia.CalcularProximaOcorrencia();
        return resultado.Sucesso ? resultado.Valor : null;
    }
}
