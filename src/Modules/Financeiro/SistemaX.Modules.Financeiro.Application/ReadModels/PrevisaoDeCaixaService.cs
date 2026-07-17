using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Application.Quant;
using SistemaX.Modules.Financeiro.Domain.Comum;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um dia da banda de previsão — espelha <see cref="BandasDeFluxoDeCaixa.BandaDia"/> mas
/// com <see cref="Data"/> (data de calendário) em vez de offset, pronto pra UI.</summary>
public sealed record BandaDeCaixaResultado(DateOnly Data, long P5Centavos, long P50Centavos, long P95Centavos);

public sealed record PrevisaoDeCaixaResultado(
    IReadOnlyList<BandaDeCaixaResultado> Bandas,
    double ProbabilidadeSaldoNegativoEm30Dias,
    DateOnly? PrimeiroDiaP50Negativo,
    int? DiasRunwayBruto,
    int? DiasRunwayRealista);

/// <summary>
/// Orquestra o motor quant (<see cref="BandasDeFluxoDeCaixa"/> + <see cref="RunwayCalculator"/>)
/// sobre os ports reais do Financeiro — catálogo #1 (bandas P5/P50/P95) e #2 (runway) do plano de
/// inteligência do Financeiro (docs/financeiro/inteligencia-arquitetura.md/ADR-0005).
///
/// INSUMOS:
/// - Histórico de "ruído" (para o bootstrap): <c>fato_caixa_diario</c> dos últimos
///   <see cref="DiasDeHistoricoParaOBootstrap"/> dias (saldo do dia = entradas − saídas).
/// - Fluxo CONHECIDO futuro: parcelas de <c>ContaAReceber</c>/<c>ContaAPagar</c> já abertas com
///   vencimento dentro do horizonte de projeção — o MESMO dado que <see cref="FluxoDeCaixaService"/>
///   já usa para a projeção determinística "ingênua"; aqui vira o componente sem incerteza da
///   simulação.
/// - Seed determinística: <see cref="SeedDeterministico.Gerar"/> de <c>businessId + dia de hoje</c>
///   — reprodutível dentro do mesmo dia (mesma consulta, mesmo resultado byte-a-byte); muda a cada
///   dia porque o "hoje" muda (não é uma seed eterna fixa, é fixa POR PERÍODO, como o ADR pede).
/// </summary>
public sealed class PrevisaoDeCaixaService(
    IFatoCaixaDiarioRepository fatoCaixaDiario,
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IMovimentoFinanceiroRepository movimentos,
    IRelogio relogio)
{
    private const int DiasDeHistoricoParaOBootstrap = 90;

    public async Task<PrevisaoDeCaixaResultado> CalcularAsync(string businessId, int diasProjecao, CancellationToken ct = default)
    {
        var agora = relogio.Agora();
        var hoje = DateOnly.FromDateTime(agora.UtcDateTime);
        var inicioHistorico = hoje.AddDays(-DiasDeHistoricoParaOBootstrap);
        var fimProjecao = hoje.AddDays(diasProjecao);

        var historicoDeltas = await CarregarHistoricoDensoAsync(businessId, inicioHistorico, hoje, ct).ConfigureAwait(false);
        var saldoAtual = await movimentos.CalcularSaldoAsync(businessId, null, agora, ct).ConfigureAwait(false);

        var fluxoConhecido = await CarregarFluxoConhecidoAsync(businessId, hoje, fimProjecao, ct).ConfigureAwait(false);

        var seed = SeedDeterministico.Gerar(businessId, hoje.ToString("yyyy-MM-dd"));
        var simulacao = BandasDeFluxoDeCaixa.Simular(historicoDeltas, saldoAtual.Centavos, fluxoConhecido, diasProjecao, seed);

        var burnEwma = RunwayCalculator.CalcularBurnDiarioEwma(historicoDeltas);
        var runway = RunwayCalculator.Calcular(saldoAtual.Centavos, burnEwma, simulacao.PrimeiroDiaOffsetP50Negativo);

        var bandas = simulacao.Bandas
            .Select(b => new BandaDeCaixaResultado(hoje.AddDays(b.DiaOffset), b.P5Centavos, b.P50Centavos, b.P95Centavos))
            .ToList();

        return new PrevisaoDeCaixaResultado(
            bandas,
            simulacao.ProbabilidadeSaldoNegativoEm30Dias,
            simulacao.PrimeiroDiaOffsetP50Negativo is { } offset ? hoje.AddDays(offset) : null,
            runway.DiasRunwayBruto,
            runway.DiasRunwayRealista);
    }

    /// <summary>Materializa a série diária de <c>fato_caixa_diario</c> como array DENSO (um valor
    /// por dia do período, 0 nos dias sem nenhum movimento) — o bootstrap em blocos precisa de uma
    /// série contígua, não de um esparso "só os dias com dado".</summary>
    private async Task<IReadOnlyList<long>> CarregarHistoricoDensoAsync(string businessId, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var fatos = await fatoCaixaDiario.ListarAsync(businessId, de, ate, ct).ConfigureAwait(false);
        var porDia = fatos.ToDictionary(f => f.Dia, f => f.SaldoDiaCentavos);

        var resultado = new List<long>();
        for (var dia = de; dia <= ate; dia = dia.AddDays(1))
        {
            resultado.Add(porDia.GetValueOrDefault(dia, 0));
        }

        return resultado;
    }

    private async Task<IReadOnlyList<BandasDeFluxoDeCaixa.PontoConhecido>> CarregarFluxoConhecidoAsync(
        string businessId, DateOnly hoje, DateOnly fimProjecao, CancellationToken ct)
    {
        var contasReceberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, fimProjecao.FimDoDia(), ct).ConfigureAwait(false);
        var contasPagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, fimProjecao.FimDoDia(), ct).ConfigureAwait(false);

        var porDia = new Dictionary<int, long>();

        void Acumular(IEnumerable<Domain.ContasAPagarReceber.Parcela> parcelas, int sinal)
        {
            foreach (var parcela in parcelas)
            {
                if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;

                var diaVencimento = DateOnly.FromDateTime(parcela.Vencimento.UtcDateTime);
                var offset = diaVencimento.DayNumber - hoje.DayNumber;
                if (offset < 1 || diaVencimento > fimProjecao) continue; // já passou ou fora do horizonte

                var restante = (parcela.Valor - parcela.ValorPago).Centavos * sinal;
                porDia[offset] = porDia.GetValueOrDefault(offset, 0) + restante;
            }
        }

        Acumular(contasReceberAbertas.SelectMany(c => c.Parcelas), sinal: 1);
        Acumular(contasPagarAbertas.SelectMany(c => c.Parcelas), sinal: -1);

        return porDia.Select(kv => new BandasDeFluxoDeCaixa.PontoConhecido(kv.Key, kv.Value)).ToList();
    }
}
