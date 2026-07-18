using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Application.Tempo;

/// <summary>Uma linha de <c>porProjeto</c>/<c>porCliente</c> do resumo (design §9.7). SIMPLIFICAÇÃO
/// DESTA FATIA: <see cref="CustoCentavos"/> é sempre <c>null</c> (decisão travada do dono — nenhum
/// apontamento tem <c>CustoHoraCentavosSnapshot</c> resolvido ainda), então o cruzamento com margem
/// (<c>indiceGargalo = custoTempo/MC1</c> do design) fica indefinido — o "índice de gargalo" desta
/// fatia é a própria ORDENAÇÃO por minutos desc (design §9.7: "null quando não há custo/hora
/// configurado — ordena por minutos nesse caso"), sem inventar uma métrica em cima de custo nulo.</summary>
public sealed record ResumoDeTempoPorProjeto(string ProjetoId, string ProjetoNome, int Minutos, long? CustoCentavos);

public sealed record ResumoDeTempoPorCliente(string ClienteId, string? ClienteNome, int Minutos, long? CustoCentavos);

public sealed record ResumoDeTempoResultado(
    DateTimeOffset De, DateTimeOffset Ate, int MinutosTotais, long? CustoTotalCentavos,
    IReadOnlyList<ResumoDeTempoPorProjeto> PorProjeto, IReadOnlyList<ResumoDeTempoPorCliente> PorCliente);

/// <summary>
/// <c>GET /financeiro/tempo/resumo</c> (design §9.7) — "onde vai meu tempo": agrega apontamentos da
/// janela por projeto e por cliente, ordenado por minutos desc (o índice de gargalo desta fatia — ver
/// nota de <see cref="ResumoDeTempoPorProjeto"/>). Índice de GARGALO por excelência: quem consome mais
/// tempo aparece primeiro, independente de pagar por ele ou não.
/// </summary>
public sealed class ResumoDeTempoService(IApontamentoDeTempoRepository apontamentos, IProjetoRepository projetos)
{
    public async Task<ResumoDeTempoResultado> CalcularAsync(string businessId, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct = default)
    {
        var lista = await apontamentos.ListarAsync(businessId, de, ate, ct: ct).ConfigureAwait(false);

        var minutosTotais = lista.Sum(a => a.Minutos);
        var custoTotal = SomarCustoOuNulo(lista);

        var nomesDeProjeto = (await projetos.ListarAsync(businessId, incluirArquivados: true, ct).ConfigureAwait(false))
            .ToDictionary(p => p.Id, p => p.Nome);

        var porProjeto = lista
            .Where(a => a.ProjetoId is not null)
            .GroupBy(a => a.ProjetoId!)
            .Select(g => new ResumoDeTempoPorProjeto(g.Key, nomesDeProjeto.GetValueOrDefault(g.Key, g.Key), g.Sum(a => a.Minutos), SomarCustoOuNulo(g.ToList())))
            .OrderByDescending(p => p.Minutos)
            .ToList();

        var porCliente = lista
            .Where(a => a.ClienteId is not null)
            .GroupBy(a => a.ClienteId!)
            .Select(g => new ResumoDeTempoPorCliente(g.Key, g.First().ClienteNome, g.Sum(a => a.Minutos), SomarCustoOuNulo(g.ToList())))
            .OrderByDescending(c => c.Minutos)
            .ToList();

        return new ResumoDeTempoResultado(de, ate, minutosTotais, custoTotal, porProjeto, porCliente);
    }

    /// <summary>Só soma se TODOS tiverem custo resolvido — um grupo com qualquer apontamento sem
    /// custo/hora configurado devolve <c>null</c> (nunca soma parcial disfarçada de total).</summary>
    private static long? SomarCustoOuNulo(IReadOnlyList<ApontamentoDeTempo> grupo)
        => grupo.Any(a => a.CustoCentavos is null) ? null : grupo.Sum(a => a.CustoCentavos!.Value);
}
