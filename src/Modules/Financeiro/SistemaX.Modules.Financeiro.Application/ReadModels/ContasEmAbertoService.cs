using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um balde de aging (0–15/15–30/+30 dias) do total ATRASADO a receber.</summary>
public sealed record AgingBucket(string Id, string Label, Money Valor);

public sealed record ContasEmAbertoResultado(
    Money ReceberEmAberto, Money ReceberAtrasado, Money PagarEmAberto, IReadOnlyList<AgingBucket> AgingBuckets);

/// <summary>
/// Card "Contas em aberto" da tela Relatórios (docs/wiring/financeiro-telas-restantes.md §B/§8):
/// soma o que ainda está pendente de <see cref="Domain.ContasAPagarReceber.ContaAReceber"/>/
/// <see cref="Domain.ContasAPagarReceber.ContaAPagar"/>, sem entidade nem serviço novo — todo o
/// dado-base já existe via <see cref="IContaAReceberRepository.ListarAbertasAteAsync"/>/
/// <see cref="IContaAPagarRepository.ListarAbertasAteAsync"/>. "Em aberto" aqui é INDEPENDENTE de
/// vencimento (inclui parcela futura tanto quanto atrasada) — por isso a consulta usa um horizonte
/// bem à frente (<see cref="HorizonteSemLimitePraticoEmAnos"/>) como upper bound, não os próximos
/// N dias de <c>FluxoDeCaixaService</c>. Os 3 baldes de aging somam exatamente
/// <see cref="ReceberAtrasado"/> (nunca escrevem outro total ao lado do que a régua mostra).
/// </summary>
public sealed class ContasEmAbertoService(IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar, IRelogio relogio)
{
    private const int HorizonteSemLimitePraticoEmAnos = 10;

    public async Task<ContasEmAbertoResultado> CalcularAsync(string businessId, CancellationToken ct = default)
    {
        var hoje = relogio.Agora();
        var semLimitePratico = hoje.AddYears(HorizonteSemLimitePraticoEmAnos);

        var receberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, semLimitePratico, ct).ConfigureAwait(false);
        var pagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, semLimitePratico, ct).ConfigureAwait(false);

        var parcelasReceberAbertas = receberAbertas
            .SelectMany(c => c.Parcelas)
            .Where(EstaEmAberto)
            .ToList();
        var parcelasPagarAbertas = pagarAbertas
            .SelectMany(c => c.Parcelas)
            .Where(EstaEmAberto)
            .ToList();

        var receberEmAberto = Somar(parcelasReceberAbertas);
        var pagarEmAberto = Somar(parcelasPagarAbertas);

        var atrasadas = parcelasReceberAbertas.Where(p => p.Status == StatusFinanceiro.Atrasado).ToList();
        var receberAtrasado = Somar(atrasadas);

        var buckets = new[]
        {
            new AgingBucket("0-15", "0–15 dias", Somar(atrasadas.Where(p => DiasAtraso(p, hoje) <= 15))),
            new AgingBucket("15-30", "15–30 dias", Somar(atrasadas.Where(p => DiasAtraso(p, hoje) is > 15 and <= 30))),
            new AgingBucket("30+", "+30 dias", Somar(atrasadas.Where(p => DiasAtraso(p, hoje) > 30))),
        };

        return new ContasEmAbertoResultado(receberEmAberto, receberAtrasado, pagarEmAberto, buckets);
    }

    private static bool EstaEmAberto(Domain.ContasAPagarReceber.Parcela p)
        => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado;

    private static int DiasAtraso(Domain.ContasAPagarReceber.Parcela p, DateTimeOffset hoje)
        => Math.Max(0, (hoje.UtcDateTime.Date - p.Vencimento.UtcDateTime.Date).Days);

    private static Money Somar(IEnumerable<Domain.ContasAPagarReceber.Parcela> parcelas)
        => parcelas.Aggregate(Money.Zero, (acumulado, p) => acumulado + (p.Valor - p.ValorPago));
}
