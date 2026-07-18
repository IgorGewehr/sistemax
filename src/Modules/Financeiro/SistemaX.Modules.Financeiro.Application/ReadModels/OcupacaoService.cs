using SistemaX.Modules.Financeiro.Application.Ports;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

public sealed record LinhaOcupacao(
    string OperadorId, string OperadorNome, int MinutosApontados, decimal HorasApontadas, decimal HorasDisponiveis, decimal ProdutividadePercent);

/// <summary>
/// LENTE VERTICAL SERVIÇOS/BELEZA (opt-in) — produtividade = horas apontadas ÷ horas disponíveis,
/// por profissional. ZERO DADO NOVO: reusa <see cref="Domain.Tempo.ApontamentoDeTempo"/> (já
/// existente para a Análise por Projeto — <see cref="Tempo.ResumoDeTempoService"/> agrupa a mesma
/// coleção por projeto/cliente; aqui agrupamos por <c>OperadorId</c>/<c>OperadorNome</c>, o mesmo
/// campo que já identifica "quem apontou" — em salão/consultório pequeno, quem atende é quem
/// aponta).
///
/// "HORAS DISPONÍVEIS" não tem cadastro de agenda/turno hoje (não existe entidade de escala de
/// profissional no sistema) — em vez de inventar uma entidade nova só pra esta lente opt-in (o que
/// violaria "nunca nova complexidade no núcleo"), o CHAMADOR informa a capacidade diária esperada
/// (<paramref name="horasDisponiveisPorDia"/> em <see cref="CalcularAsync"/>, default 8h) e o
/// serviço multiplica pelos DIAS CORRIDOS da janela pedida — a mesma simplicidade de "dias no mês"
/// que <c>BreakevenMensal</c> já usa em vez de calendário de dias úteis.
///
/// OPT-IN por presença de dado: sem apontamento nenhum no período, a lista vem vazia (fail-quiet —
/// a lente simplesmente não aparece pra quem não usa apontamento de tempo).
/// </summary>
public sealed class OcupacaoService(IApontamentoDeTempoRepository apontamentos)
{
    private const decimal HorasDisponiveisPorDiaPadrao = 8m;

    public async Task<IReadOnlyList<LinhaOcupacao>> CalcularAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, decimal? horasDisponiveisPorDia = null, CancellationToken ct = default)
    {
        var capacidadeDiaria = horasDisponiveisPorDia is > 0 ? horasDisponiveisPorDia.Value : HorasDisponiveisPorDiaPadrao;
        var dias = Math.Max(1, (decimal)Math.Max(1, (ate.Date - de.Date).Days));
        var horasDisponiveis = capacidadeDiaria * dias;

        var lista = await apontamentos.ListarAsync(businessId, de, ate, ct: ct).ConfigureAwait(false);

        return lista
            .GroupBy(a => (a.OperadorId, a.OperadorNome))
            .Select(grupo =>
            {
                var minutos = grupo.Sum(a => a.Minutos);
                var horas = Math.Round(minutos / 60m, 2);
                var produtividade = horasDisponiveis > 0 ? Math.Round(100m * horas / horasDisponiveis, 1) : 0m;
                return new LinhaOcupacao(grupo.Key.OperadorId, grupo.Key.OperadorNome, minutos, horas, horasDisponiveis, produtividade);
            })
            .OrderByDescending(linha => linha.ProdutividadePercent)
            .ToList();
    }
}
