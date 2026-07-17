using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;

namespace SistemaX.Modules.Financeiro.Application.Caixa;

/// <summary>
/// <see cref="MovimentoFinanceiro"/> não carrega descrição própria — é um fato de CAIXA puro
/// (docs/financeiro-datamodel.md §3). A descrição humana ("Venda venda-123", "NF fornecedor-456")
/// vive na <c>ContaAReceber</c>/<c>ContaAPagar</c> de competência apontada por
/// <see cref="MovimentoFinanceiro.ContaOrigemId"/> — este resolvedor é o LAR ÚNICO dessa junção,
/// usado pelo extrato do Bancário e pelo painel de conciliação
/// (docs/wiring/financeiro-telas-restantes.md §3) para nunca inventar um texto que o domínio não
/// tem.
///
/// <see cref="MovimentoFinanceiro.Tipo"/> diz qual dos dois repositórios consultar primeiro
/// (Entrada → <c>ContaAReceber</c>, Saída → <c>ContaAPagar</c> — é exatamente o que
/// <c>VendaConcluidaHandler</c>/<c>BaixarParcelaUseCase</c> gravam), com fallback no outro
/// repositório e, por fim, na <see cref="MovimentoFinanceiro.Origem"/> (módulo + id do fato de
/// integração) quando nenhuma conta de competência for encontrada (ex.: dado de teste/seed sem
/// conta correspondente).
/// </summary>
public sealed class ResolvedorDeDescricaoDeMovimento(IContaAReceberRepository contasAReceber, IContaAPagarRepository contasAPagar)
{
    public async Task<string> ResolverAsync(MovimentoFinanceiro movimento, CancellationToken ct = default)
    {
        if (movimento.Tipo == TipoMovimentoFinanceiro.Entrada)
        {
            var receber = await contasAReceber.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (receber is not null) return receber.Descricao;

            var pagar = await contasAPagar.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (pagar is not null) return pagar.Descricao;
        }
        else
        {
            var pagar = await contasAPagar.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (pagar is not null) return pagar.Descricao;

            var receber = await contasAReceber.ObterPorIdAsync(movimento.ContaOrigemId, ct).ConfigureAwait(false);
            if (receber is not null) return receber.Descricao;
        }

        return $"{movimento.Origem.Modulo} · {movimento.Origem.Id}";
    }
}
