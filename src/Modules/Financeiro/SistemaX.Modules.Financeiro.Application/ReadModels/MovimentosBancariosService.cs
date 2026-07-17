using SistemaX.Modules.Financeiro.Application.Caixa;
using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.ReadModels;

/// <summary>Uma linha do extrato da tela Bancário — <see cref="Valor"/> já vem COM SINAL
/// (positivo para <see cref="TipoMovimentoFinanceiro.Entrada"/>, negativo para
/// <see cref="TipoMovimentoFinanceiro.Saida"/>), diferente de <c>MovimentoFinanceiro.Valor</c>
/// (sempre positivo — o sentido ali é expresso só por <c>Tipo</c>). <see cref="Conciliado"/> é
/// <c>true</c> quando existe QUALQUER <c>Conciliacao</c> confirmada (auto ou manual) para este
/// movimento.</summary>
public sealed record MovimentoBancarioResumo(
    string Id, DateTimeOffset Data, string Descricao, string Forma, string ContaBancariaCaixaId, Money Valor, bool Conciliado);

/// <summary>
/// Painel de EXTRATO (a tela Bancário — docs/wiring/financeiro-telas-restantes.md §3): junta o
/// fato de caixa (<see cref="MovimentoFinanceiro"/>) com o nome da forma de pagamento e o status
/// de conciliação, exatamente o que <c>GET /financeiro/movimentos</c> precisa devolver.
/// </summary>
public sealed class MovimentosBancariosService(
    IMovimentoFinanceiroRepository movimentos,
    IFormaDePagamentoRepository formas,
    IConciliacaoRepository conciliacoes,
    ResolvedorDeDescricaoDeMovimento resolvedorDescricao)
{
    public async Task<IReadOnlyList<MovimentoBancarioResumo>> ListarAsync(
        string businessId, DateTimeOffset inicio, DateTimeOffset fim, string? contaBancariaCaixaId = null, CancellationToken ct = default)
    {
        var doPeriodo = await movimentos.ListarPorPeriodoAsync(businessId, inicio, fim, ct).ConfigureAwait(false);
        if (contaBancariaCaixaId is not null)
        {
            doPeriodo = doPeriodo.Where(m => m.ContaBancariaCaixaId == contaBancariaCaixaId).ToList();
        }

        var conciliadosDoNegocio = await conciliacoes.ListarPorBusinessIdAsync(businessId, ct).ConfigureAwait(false);
        var movimentosConciliados = conciliadosDoNegocio
            .Where(c => c.Status is StatusConciliacao.ConciliadoAuto or StatusConciliacao.ConciliadoManual)
            .Select(c => c.MovimentoFinanceiroId)
            .ToHashSet();

        var nomesDeForma = new Dictionary<string, string>();

        var resultado = new List<MovimentoBancarioResumo>(doPeriodo.Count);
        foreach (var movimento in doPeriodo.OrderByDescending(m => m.DataMovimento))
        {
            if (!nomesDeForma.TryGetValue(movimento.FormaPagamentoId, out var nomeForma))
            {
                var forma = await formas.ObterPorIdAsync(businessId, movimento.FormaPagamentoId, ct).ConfigureAwait(false);
                nomeForma = forma?.Nome ?? movimento.FormaPagamentoId;
                nomesDeForma[movimento.FormaPagamentoId] = nomeForma;
            }

            var descricao = await resolvedorDescricao.ResolverAsync(movimento, ct).ConfigureAwait(false);
            var valorComSinal = movimento.Tipo == TipoMovimentoFinanceiro.Entrada ? movimento.Valor : -movimento.Valor;

            resultado.Add(new MovimentoBancarioResumo(
                movimento.Id, movimento.DataMovimento, descricao, nomeForma, movimento.ContaBancariaCaixaId,
                valorComSinal, movimentosConciliados.Contains(movimento.Id)));
        }

        return resultado;
    }
}
