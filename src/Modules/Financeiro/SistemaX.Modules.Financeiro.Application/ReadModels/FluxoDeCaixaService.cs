using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Um dia do fluxo de caixa — <see cref="Projetado"/> distingue realizado de previsto (docs/financeiro-features.md §4.10).</summary>
public sealed record PontoFluxoCaixa(DateOnly Data, Money Entradas, Money Saidas, Money SaldoAcumulado, bool Projetado);

public sealed record FluxoDeCaixaResultado(IReadOnlyList<PontoFluxoCaixa> Pontos, DateOnly? PrimeiroDiaNegativo);

/// <summary>
/// View de FLUXO DE CAIXA — realizado (soma diária de <c>MovimentoFinanceiro</c>, regime de
/// caixa) + projetado (parcelas em aberto por vencimento, regime de competência ainda não
/// liquidado). É a feature nº1 de prevenção de falência do módulo (docs/financeiro-features.md
/// §4.2): o primeiro dia em que o saldo projetado cruza zero é o dado mais acionável do
/// dashboard. Não persiste nada — é uma view derivada recomputada a cada chamada (R6 do CLAUDE.md
/// do repo-irmão: views derivadas, não novas coleções gravadas).
/// </summary>
public sealed class FluxoDeCaixaService(
    IMovimentoFinanceiroRepository movimentos,
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IRelogio relogio)
{
    public async Task<FluxoDeCaixaResultado> ProjetarAsync(string businessId, int diasHistorico, int diasProjecao, CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(relogio.Agora().UtcDateTime);
        var inicioHistorico = hoje.AddDays(-Math.Abs(diasHistorico));
        var fimProjecao = hoje.AddDays(Math.Abs(diasProjecao));

        var movimentosPeriodo = await movimentos.ListarPorPeriodoAsync(businessId, inicioHistorico.InicioDoDia(), hoje.FimDoDia(), ct);
        var saldoAntesDoPeriodo = await movimentos.CalcularSaldoAsync(businessId, null, inicioHistorico.InicioDoDia().AddTicks(-1), ct);

        var pontos = new List<PontoFluxoCaixa>();
        var saldoAcumulado = saldoAntesDoPeriodo;

        for (var dia = inicioHistorico; dia <= hoje; dia = dia.AddDays(1))
        {
            var movimentosDoDia = movimentosPeriodo.Where(m => DateOnly.FromDateTime(m.DataMovimento.UtcDateTime) == dia).ToList();
            var entradas = movimentosDoDia.Where(m => m.Tipo == TipoMovimentoFinanceiro.Entrada).Aggregate(Money.Zero, (acumulado, m) => acumulado + m.Valor);
            var saidas = movimentosDoDia.Where(m => m.Tipo == TipoMovimentoFinanceiro.Saida).Aggregate(Money.Zero, (acumulado, m) => acumulado + m.Valor);

            saldoAcumulado = saldoAcumulado + entradas - saidas;
            pontos.Add(new PontoFluxoCaixa(dia, entradas, saidas, saldoAcumulado, Projetado: false));
        }

        var contasReceberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, fimProjecao.FimDoDia(), ct);
        var contasPagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, fimProjecao.FimDoDia(), ct);

        for (var dia = hoje.AddDays(1); dia <= fimProjecao; dia = dia.AddDays(1))
        {
            var entradasPrevistas = SaldoRestanteNoDia(contasReceberAbertas.SelectMany(c => c.Parcelas), dia);
            var saidasPrevistas = SaldoRestanteNoDia(contasPagarAbertas.SelectMany(c => c.Parcelas), dia);

            saldoAcumulado = saldoAcumulado + entradasPrevistas - saidasPrevistas;
            pontos.Add(new PontoFluxoCaixa(dia, entradasPrevistas, saidasPrevistas, saldoAcumulado, Projetado: true));
        }

        var primeiroDiaNegativo = pontos.FirstOrDefault(p => p.Projetado && p.SaldoAcumulado.EhNegativo)?.Data;
        return new FluxoDeCaixaResultado(pontos, primeiroDiaNegativo);
    }

    private static Money SaldoRestanteNoDia(IEnumerable<Domain.ContasAPagarReceber.Parcela> parcelas, DateOnly dia)
        => parcelas
            .Where(p => p.Status is StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)
            .Where(p => DateOnly.FromDateTime(p.Vencimento.UtcDateTime) == dia)
            .Aggregate(Money.Zero, (acumulado, p) => acumulado + (p.Valor - p.ValorPago));
}
