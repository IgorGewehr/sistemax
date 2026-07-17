using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Regras;

/// <summary>
/// CSOSN (Simples) OU CST (Normal) — nunca os dois ao mesmo tempo. As factories impõem a
/// compatibilidade com o regime; não existe construtor público que deixe montar um CSOSN para
/// regime Normal ou vice-versa (docs/fiscal/arquitetura.md §2.3).
/// </summary>
public sealed record SituacaoTributariaIcms
{
    public string Codigo { get; }
    public bool EhCsosn { get; }

    private SituacaoTributariaIcms(string codigo, bool ehCsosn) => (Codigo, EhCsosn) = (codigo, ehCsosn);

    public static Result<SituacaoTributariaIcms> ParaCsosn(RegimeTributario regime, string codigo)
    {
        if (!regime.UsaCsosn())
            return Result.Falhar<SituacaoTributariaIcms>(new Error(
                "fiscal.situacao.csosn_fora_do_simples", $"CSOSN não se aplica ao regime '{regime}'."));
        return Result.Ok(new SituacaoTributariaIcms(codigo, true));
    }

    public static Result<SituacaoTributariaIcms> ParaCst(RegimeTributario regime, string codigo)
    {
        if (regime.UsaCsosn())
            return Result.Falhar<SituacaoTributariaIcms>(new Error(
                "fiscal.situacao.cst_fora_do_normal", $"CST não se aplica ao regime '{regime}' (use CSOSN)."));
        return Result.Ok(new SituacaoTributariaIcms(codigo, false));
    }
}
