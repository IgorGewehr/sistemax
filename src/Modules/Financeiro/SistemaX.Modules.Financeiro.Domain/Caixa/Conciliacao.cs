using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

public enum StatusConciliacao
{
    NaoConciliado,
    ConciliadoAuto,
    ConciliadoManual,
    Ignorado
}

/// <summary>
/// Vínculo entre um <c>MovimentoFinanceiro</c> (interno) e um <c>ExtratoBancarioItem</c>
/// (linha importada do banco) — é onde fraude, taxa cobrada errado e venda não lançada aparecem
/// (docs/financeiro-features.md §4.5).
/// </summary>
public sealed class Conciliacao : Entity<string>
{
    public string BusinessId { get; }
    public string MovimentoFinanceiroId { get; }
    public string ExtratoBancarioItemId { get; }
    public StatusConciliacao Status { get; private set; }
    public DateTimeOffset? ConciliadoEm { get; private set; }

    private Conciliacao(string id, string businessId, string movimentoFinanceiroId, string extratoBancarioItemId)
    {
        Id = id;
        BusinessId = businessId;
        MovimentoFinanceiroId = movimentoFinanceiroId;
        ExtratoBancarioItemId = extratoBancarioItemId;
        Status = StatusConciliacao.NaoConciliado;
    }

    public static Conciliacao Criar(string businessId, string movimentoFinanceiroId, string extratoBancarioItemId)
        => new(IdGenerator.NovoId(), businessId, movimentoFinanceiroId, extratoBancarioItemId);

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static Conciliacao Reconstituir(
        string id, string businessId, string movimentoFinanceiroId, string extratoBancarioItemId,
        StatusConciliacao status, DateTimeOffset? conciliadoEm)
    {
        var conciliacao = new Conciliacao(id, businessId, movimentoFinanceiroId, extratoBancarioItemId);
        conciliacao.Status = status;
        conciliacao.ConciliadoEm = conciliadoEm;
        return conciliacao;
    }

    public Result Confirmar(bool automatico, DateTimeOffset agora)
    {
        if (Status is StatusConciliacao.ConciliadoAuto or StatusConciliacao.ConciliadoManual)
            return Result.Ok(); // idempotente: reconciliar de novo o mesmo par não é erro

        Status = automatico ? StatusConciliacao.ConciliadoAuto : StatusConciliacao.ConciliadoManual;
        ConciliadoEm = agora;
        return Result.Ok();
    }

    public Result Ignorar()
    {
        Status = StatusConciliacao.Ignorado;
        return Result.Ok();
    }
}
