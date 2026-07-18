using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Ativos;

/// <summary>
/// APORTE DE CAPITAL — registro LEVE, gerencial, de capital de giro/investimento inicial
/// (docs/financeiro/design-imobilizado-roi.md §3.3). Deliberadamente FORA de
/// <c>Caixa.MovimentoFinanceiro</c> (que exige <c>ParcelaId</c> — um aporte não liquida nenhuma
/// parcela) e FORA da partida dobrada (não credita <c>3.1 Receita</c>, não entra no DRE/RBT12): as
/// três alternativas avaliadas e rejeitadas (tipo novo de <c>MovimentoFinanceiro</c>, <c>ContaAReceber</c>
/// especial, conta de Patrimônio Líquido) contaminariam a série de caixa operacional que o payback
/// do Painel de ROI (<c>ReadModels.RoiDoNegocioService</c>) lê — exatamente o que §7.4 do design
/// prova que NÃO pode acontecer (o aporte declarado nunca move a data do payback; só o denominador
/// do ROI% e a leitura de funding).
///
/// Sem FSM, sem lançamento contábil, DELETÁVEL fisicamente (Decisão DI5 do design — mesmo racional
/// de <c>Tempo.ApontamentoDeTempo</c>: registro gerencial que alimenta UMA lente, não fato
/// contábil; errou, apaga e relança). Conta no "total investido" do ROI e em NADA mais.
/// </summary>
public sealed class AporteDeCapital : AggregateRoot<string>
{
    public string BusinessId { get; }
    public Money Valor { get; }
    public DateOnly Data { get; }
    public string Descricao { get; }
    public DateTimeOffset CriadoEm { get; }

    private AporteDeCapital(string id, string businessId, Money valor, DateOnly data, string descricao, DateTimeOffset criadoEm)
    {
        Id = id;
        BusinessId = businessId;
        Valor = valor;
        Data = data;
        Descricao = descricao;
        CriadoEm = criadoEm;
    }

    public static Result<AporteDeCapital> Criar(string businessId, Money valor, DateOnly data, string descricao, DateTimeOffset criadoEm)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<AporteDeCapital>(new Error("financeiro.aporte.business_obrigatorio", "BusinessId é obrigatório."));

        if (!valor.EhPositivo)
            return Result.Falhar<AporteDeCapital>(new Error("financeiro.aporte.valor_invalido", "Valor do aporte deve ser positivo."));

        if (string.IsNullOrWhiteSpace(descricao))
            return Result.Falhar<AporteDeCapital>(new Error("financeiro.aporte.descricao_obrigatoria", "Descrição do aporte é obrigatória."));

        return Result.Ok(new AporteDeCapital(IdGenerator.NovoId(), businessId, valor, data, descricao.Trim(), criadoEm));
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static AporteDeCapital Reconstituir(string id, string businessId, Money valor, DateOnly data, string descricao, DateTimeOffset criadoEm)
        => new(id, businessId, valor, data, descricao, criadoEm);
}
