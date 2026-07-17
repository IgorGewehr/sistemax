using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// Estorno de um fato JÁ LIQUIDADO (docs/financeiro-datamodel.md §4.4): gera um novo
/// <see cref="MovimentoFinanceiro"/> de sinal invertido + um novo <see cref="LancamentoContabil"/>
/// de partidas espelhadas, ambos apontando para o original via <c>ReversalOfId</c>. NUNCA edita
/// nem apaga o original. Idempotente: reprocessar o mesmo estorno (mesmo <c>movimentoOriginalId</c>)
/// não duplica — <see cref="IMovimentoFinanceiroRepository.BuscarEstornoDeAsync"/> é o guard.
/// </summary>
public sealed class EstornarMovimentoUseCase(
    IMovimentoFinanceiroRepository movimentos,
    ILancamentoContabilRepository lancamentos)
{
    public async Task<Result<MovimentoFinanceiro>> ExecutarAsync(
        string movimentoOriginalId, DateTimeOffset dataEstorno, SourceRef origemEstorno, string motivo, CancellationToken ct = default)
    {
        var estornoExistente = await movimentos.BuscarEstornoDeAsync(movimentoOriginalId, ct);
        if (estornoExistente is not null) return Result.Ok(estornoExistente);

        var original = await movimentos.ObterPorIdAsync(movimentoOriginalId, ct);
        if (original is null)
            return Result.Falhar<MovimentoFinanceiro>(new Error("financeiro.movimento.nao_encontrado", $"MovimentoFinanceiro '{movimentoOriginalId}' não encontrado."));

        var estornoResultado = original.GerarEstorno(dataEstorno, origemEstorno);
        if (estornoResultado.Falha) return estornoResultado;

        await movimentos.SalvarAsync(estornoResultado.Valor, ct);

        var origemLancamentoOriginal = original.Tipo == TipoMovimentoFinanceiro.Entrada
            ? new OrigemLancamento("financeiro", "movimento-entrada", original.Id)
            : new OrigemLancamento("financeiro", "movimento-saida", original.Id);

        var lancamentoOriginal = await lancamentos.BuscarPorOrigemAsync(original.BusinessId, origemLancamentoOriginal.Chave, ct);
        if (lancamentoOriginal is null)
            return Result.Falhar<MovimentoFinanceiro>(new Error(
                "financeiro.lancamento.nao_encontrado",
                $"Lançamento contábil original do movimento '{movimentoOriginalId}' não encontrado — dado inconsistente entre camada de caixa e camada contábil."));

        var lancamentoEstornoResultado = lancamentoOriginal.GerarEstorno(dataEstorno, motivo);
        if (lancamentoEstornoResultado.Falha) return Result.Falhar<MovimentoFinanceiro>(lancamentoEstornoResultado.Erro);

        await lancamentos.SalvarAsync(lancamentoEstornoResultado.Valor, ct);

        return estornoResultado;
    }
}
