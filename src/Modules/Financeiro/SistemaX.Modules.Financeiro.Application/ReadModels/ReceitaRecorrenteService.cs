using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>MRR de um serviço + seu peso na receita recorrente (concentração).</summary>
public sealed record MrrPorServico(string ServicoId, string ServicoNome, Money Mrr, decimal Percentual);

/// <summary>
/// Painel de RECEITA RECORRENTE (a tela Assinaturas): MRR, ARR, novos/churn do mês, churn %,
/// ticket médio, MRR por serviço e a maior concentração (o gargalo mais perigoso — 1 cliente/
/// serviço carregando a receita). Tudo DERIVADO das assinaturas, nada persistido.
/// </summary>
public sealed record ReceitaRecorrenteResultado(
    Money Mrr,
    Money Arr,
    int AssinaturasAtivas,
    Money TicketMedio,
    Money MrrNovoNoMes,
    Money MrrChurnNoMes,
    int ClientesChurnNoMes,
    decimal ChurnPercent,
    IReadOnlyList<MrrPorServico> PorServico,
    MrrPorServico? MaiorConcentracao);

public sealed class ReceitaRecorrenteService(IAssinaturaRepository assinaturas)
{
    public async Task<ReceitaRecorrenteResultado> CalcularAsync(string businessId, DateTimeOffset referencia, CancellationToken ct = default)
    {
        var todas = await assinaturas.ListarAsync(businessId, ct);
        var ativas = todas.Where(a => a.Status == StatusAssinatura.Ativa).ToList();

        var mrr = ativas.Aggregate(Money.Zero, (acc, a) => acc + a.Mrr);
        var arr = mrr * 12;
        var ticket = ativas.Count == 0 ? Money.Zero : new Money(mrr.Centavos / ativas.Count);

        var inicioMes = new DateTimeOffset(referencia.Year, referencia.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var fimMes = inicioMes.AddMonths(1);

        var mrrNovo = ativas
            .Where(a => a.DataInicio >= inicioMes && a.DataInicio < fimMes)
            .Aggregate(Money.Zero, (acc, a) => acc + a.Mrr);

        var churnadas = todas
            .Where(a => a.Status == StatusAssinatura.Cancelada && a.CanceladaEm is { } c && c >= inicioMes && c < fimMes)
            .ToList();
        var mrrChurn = churnadas.Aggregate(Money.Zero, (acc, a) => acc + a.Mrr);

        // Churn % de receita = MRR perdido / MRR no INÍCIO do mês (= ativo agora − novos + perdidos).
        var mrrInicioMes = mrr.Centavos - mrrNovo.Centavos + mrrChurn.Centavos;
        var churnPct = mrrInicioMes <= 0 ? 0m : Math.Round((decimal)mrrChurn.Centavos / mrrInicioMes * 100m, 1);

        var porServico = ativas
            .GroupBy(a => (a.ServicoId, a.ServicoNome))
            .Select(g =>
            {
                var m = g.Aggregate(Money.Zero, (acc, a) => acc + a.Mrr);
                var pct = mrr.EhZero ? 0m : Math.Round((decimal)m.Centavos / mrr.Centavos * 100m, 1);
                return new MrrPorServico(g.Key.ServicoId, g.Key.ServicoNome, m, pct);
            })
            .OrderByDescending(x => x.Mrr.Centavos)
            .ToList();

        return new ReceitaRecorrenteResultado(
            mrr, arr, ativas.Count, ticket, mrrNovo, mrrChurn, churnadas.Count, churnPct, porServico, porServico.FirstOrDefault());
    }
}
