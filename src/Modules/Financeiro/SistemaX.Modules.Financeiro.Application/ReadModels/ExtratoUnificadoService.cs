using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma linha do extrato unificado da tela Entradas &amp; Saídas
/// (docs/wiring/financeiro-telas-restantes.md §1 — "Linha do tempo": o comentário do próprio
/// mockup já antecipa "ExtratoUnificado: MovimentoFinanceiro + Parcela"). <see cref="Data"/> é a
/// data de VENCIMENTO para <c>previsto</c>/<c>atrasado</c> e a data de PAGAMENTO
/// (<see cref="MovimentoFinanceiro.DataMovimento"/>) para <c>pago</c> — mesma semântica do
/// mockup (linhas pagas ordenadas pela data em que o dinheiro mudou de mão, não pelo vencimento
/// original). <see cref="Conta"/>/<see cref="Origem"/> só vêm preenchidos quando <see cref="Status"/>
/// é <c>pago</c> — <see cref="Conta"/> é o nome real de <see cref="Domain.Caixa.ContaBancariaCaixa"/>,
/// <see cref="Origem"/> o nome real de <see cref="Domain.Caixa.FormaDePagamento"/> (mesmos dois
/// campos reais que já alimentam o extrato do Bancário — nunca um texto inventado como "Nota fiscal
/// de compra"/"Guia DAS" do mockup, que o domínio não tem lugar nenhum para guardar).</summary>
public sealed record LinhaExtrato(
    string Id, DateTimeOffset Data, string Descricao, string CategoriaId, string Tipo, string Status,
    Money Valor, string? Conta, string? Origem, int? DiasAtraso);

public sealed record ExtratoKpis(Money TotalEntradas, Money TotalSaidas, Money SaldoPeriodo);

public sealed record ExtratoResultado(IReadOnlyList<LinhaExtrato> Linhas, ExtratoKpis Kpis);

/// <summary>
/// Painel de EXTRATO UNIFICADO (a tela Entradas &amp; Saídas — docs/wiring/
/// financeiro-telas-restantes.md §1): junta o REALIZADO (<see cref="MovimentoFinanceiro"/>, status
/// <c>pago</c>) com o PREVISTO/ATRASADO (parcelas ainda abertas de <see cref="ContaAReceber"/>/
/// <see cref="ContaAPagar"/>) num único extrato, com KPIs de total entradas/saídas/saldo do
/// período. O "saldo projetado de fim de mês" que o mockup também mostra ("Como fecha o mês") NÃO
/// é recalculado aqui — é o mesmo saldo acumulado de <c>GET /financeiro/fluxo</c>
/// (<c>FluxoDeCaixaService</c>), reusado pelo front, não duplicado neste read-model.
///
/// <paramref name="tipo"/>/<paramref name="categoriaId"/> filtram as LINHAS devolvidas, mas os
/// KPIs (<see cref="ExtratoKpis"/>) são calculados sobre o conjunto já filtrado por
/// <paramref name="categoriaId"/> e PERÍODO, mas ANTES do filtro de <paramref name="tipo"/> — do
/// contrário "total de saídas" viraria zero sempre que o usuário filtrasse só "A receber".
/// </summary>
public sealed class ExtratoUnificadoService(
    IMovimentoFinanceiroRepository movimentos,
    IContaAReceberRepository contasAReceber,
    IContaAPagarRepository contasAPagar,
    IContaBancariaCaixaRepository contasBancarias,
    IFormaDePagamentoRepository formasDePagamento,
    IRelogio relogio)
{
    public async Task<ExtratoResultado> ListarAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate,
        string? tipo = null, string? categoriaId = null, CancellationToken ct = default)
    {
        var hoje = relogio.Agora();

        var linhasPagas = await MontarLinhasPagasAsync(businessId, de, ate, ct).ConfigureAwait(false);
        var linhasAbertas = await MontarLinhasAbertasAsync(businessId, de, ate, hoje, ct).ConfigureAwait(false);

        var todas = linhasPagas
            .Concat(linhasAbertas)
            .Where(l => categoriaId is null || l.CategoriaId == categoriaId)
            .OrderByDescending(l => l.Data)
            .ToList();

        var totalEntradas = Somar(todas.Where(l => l.Tipo == "entrada"));
        var totalSaidas = Somar(todas.Where(l => l.Tipo == "saida"));
        var kpis = new ExtratoKpis(totalEntradas, totalSaidas, totalEntradas - totalSaidas);

        var linhas = tipo is null ? todas : todas.Where(l => l.Tipo == tipo).ToList();
        return new ExtratoResultado(linhas, kpis);
    }

    private async Task<List<LinhaExtrato>> MontarLinhasPagasAsync(string businessId, DateTimeOffset de, DateTimeOffset ate, CancellationToken ct)
    {
        var doPeriodo = await movimentos.ListarPorPeriodoAsync(businessId, de, ate, ct).ConfigureAwait(false);
        var linhas = new List<LinhaExtrato>(doPeriodo.Count);
        var nomesDeConta = new Dictionary<string, string>();
        var nomesDeForma = new Dictionary<string, string>();

        foreach (var movimento in doPeriodo)
        {
            var (descricao, categoriaId) = await ResolverOrigemAsync(movimento, ct).ConfigureAwait(false);

            if (!nomesDeConta.TryGetValue(movimento.ContaBancariaCaixaId, out var nomeConta))
            {
                var conta = await contasBancarias.ObterPorIdAsync(businessId, movimento.ContaBancariaCaixaId, ct).ConfigureAwait(false);
                nomeConta = conta?.Nome ?? movimento.ContaBancariaCaixaId;
                nomesDeConta[movimento.ContaBancariaCaixaId] = nomeConta;
            }

            if (!nomesDeForma.TryGetValue(movimento.FormaPagamentoId, out var nomeForma))
            {
                var forma = await formasDePagamento.ObterPorIdAsync(businessId, movimento.FormaPagamentoId, ct).ConfigureAwait(false);
                nomeForma = forma?.Nome ?? movimento.FormaPagamentoId;
                nomesDeForma[movimento.FormaPagamentoId] = nomeForma;
            }

            linhas.Add(new LinhaExtrato(
                movimento.Id, movimento.DataMovimento, descricao, categoriaId,
                movimento.Tipo == TipoMovimentoFinanceiro.Entrada ? "entrada" : "saida",
                "pago", movimento.Valor, nomeConta, nomeForma, DiasAtraso: null));
        }

        return linhas;
    }

    private async Task<(string Descricao, string CategoriaId)> ResolverOrigemAsync(MovimentoFinanceiro movimento, CancellationToken ct)
    {
        if (movimento.Tipo == TipoMovimentoFinanceiro.Entrada)
        {
            var receber = await contasAReceber.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (receber is not null) return (receber.Descricao, receber.CategoriaId);

            var pagarFallback = await contasAPagar.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (pagarFallback is not null) return (pagarFallback.Descricao, pagarFallback.CategoriaId);
        }
        else
        {
            var pagar = await contasAPagar.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (pagar is not null) return (pagar.Descricao, pagar.CategoriaId);

            var receberFallback = await contasAReceber.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (receberFallback is not null) return (receberFallback.Descricao, receberFallback.CategoriaId);
        }

        return ($"{movimento.Origem.Modulo} · {movimento.Origem.Id}", "outros");
    }

    private async Task<List<LinhaExtrato>> MontarLinhasAbertasAsync(
        string businessId, DateTimeOffset de, DateTimeOffset ate, DateTimeOffset hoje, CancellationToken ct)
    {
        var receberAbertas = await contasAReceber.ListarAbertasAteAsync(businessId, ate, ct).ConfigureAwait(false);
        var pagarAbertas = await contasAPagar.ListarAbertasAteAsync(businessId, ate, ct).ConfigureAwait(false);

        var linhas = new List<LinhaExtrato>();
        foreach (var conta in receberAbertas)
            linhas.AddRange(ParcelasNoPeriodo(conta.Parcelas, de, ate, hoje, "entrada", conta.Descricao, conta.CategoriaId));
        foreach (var conta in pagarAbertas)
            linhas.AddRange(ParcelasNoPeriodo(conta.Parcelas, de, ate, hoje, "saida", conta.Descricao, conta.CategoriaId));

        return linhas;
    }

    private static IEnumerable<LinhaExtrato> ParcelasNoPeriodo(
        IReadOnlyList<Parcela> parcelas, DateTimeOffset de, DateTimeOffset ate, DateTimeOffset hoje,
        string tipo, string descricao, string categoriaId)
    {
        foreach (var parcela in parcelas)
        {
            if (parcela.Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial or StatusFinanceiro.Atrasado)) continue;
            if (parcela.Vencimento < de || parcela.Vencimento > ate) continue;

            var status = parcela.Status == StatusFinanceiro.Atrasado ? "atrasado" : "previsto";
            int? diasAtraso = status == "atrasado"
                ? Math.Max(0, (hoje.UtcDateTime.Date - parcela.Vencimento.UtcDateTime.Date).Days)
                : null;

            yield return new LinhaExtrato(
                parcela.Id, parcela.Vencimento, descricao, categoriaId, tipo, status,
                parcela.Valor - parcela.ValorPago, Conta: null, Origem: null, diasAtraso);
        }
    }

    private static Money Somar(IEnumerable<LinhaExtrato> linhas) => linhas.Aggregate(Money.Zero, (acumulado, l) => acumulado + l.Valor);
}
