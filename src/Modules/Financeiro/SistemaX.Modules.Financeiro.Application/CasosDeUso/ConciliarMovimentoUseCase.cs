using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.CasosDeUso;

/// <summary>
/// Cruza um <c>MovimentoFinanceiro</c> interno com uma linha de extrato bancário importada —
/// onde fraude, taxa cobrada errado e venda não lançada aparecem (docs/financeiro-features.md
/// §4.5). Idempotente: reconciliar o mesmo par duas vezes não é erro (<c>Conciliacao.Confirmar</c>
/// é no-op se já confirmado).
/// </summary>
public sealed class ConciliarMovimentoUseCase(IConciliacaoRepository conciliacoes, IRelogio relogio)
{
    public async Task<Result<Conciliacao>> ExecutarAsync(
        string businessId, string movimentoFinanceiroId, string extratoBancarioItemId, bool automatico, CancellationToken ct = default)
    {
        var conciliacao = await conciliacoes.BuscarPorParAsync(movimentoFinanceiroId, extratoBancarioItemId, ct)
                          ?? Conciliacao.Criar(businessId, movimentoFinanceiroId, extratoBancarioItemId);

        var resultado = conciliacao.Confirmar(automatico, relogio.Agora());
        if (resultado.Falha) return Result.Falhar<Conciliacao>(resultado.Erro);

        await conciliacoes.SalvarAsync(conciliacao, ct);
        return Result.Ok(conciliacao);
    }

    public async Task<Result<Conciliacao>> IgnorarAsync(string movimentoFinanceiroId, string extratoBancarioItemId, CancellationToken ct = default)
    {
        var conciliacao = await conciliacoes.BuscarPorParAsync(movimentoFinanceiroId, extratoBancarioItemId, ct);
        if (conciliacao is null)
            return Result.Falhar<Conciliacao>(new Error("financeiro.conciliacao.nao_encontrada", "Par movimento/extrato não encontrado para ignorar."));

        conciliacao.Ignorar();
        await conciliacoes.SalvarAsync(conciliacao, ct);
        return Result.Ok(conciliacao);
    }
}
