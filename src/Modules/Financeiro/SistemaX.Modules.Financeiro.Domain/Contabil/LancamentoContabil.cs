using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Eventos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// O MOTOR INVISÍVEL de partida dobrada. Header de um fato contábil com N <see cref="PartidaContabil"/>
/// filhas. INVARIANTE DURA (docs/financeiro/financeiro-datamodel.md §1, §2.3):
///
///     Σ partidas a débito == Σ partidas a crédito
///
/// Essa invariante é garantida DENTRO do agregado — a única forma de instanciar um
/// <see cref="LancamentoContabil"/> é <see cref="Criar"/>, que recusa (via <see cref="Result"/>,
/// não exceção — é uma regra de negócio esperada) qualquer conjunto de partidas desbalanceado.
/// Não existe setter público de partida: uma vez criado, um lançamento é IMUTÁVEL. Corrigir um
/// lançamento errado nunca é editar — é gerar um novo lançamento de estorno com
/// <see cref="GerarEstorno"/>, que aponta <see cref="ReversalOfId"/> para o original e espelha
/// cada partida (débito↔crédito invertidos), preservando o original intocado para sempre.
/// </summary>
public sealed class LancamentoContabil : AggregateRoot<string>
{
    private readonly List<PartidaContabil> _partidas = [];

    public string BusinessId { get; }
    public DateTimeOffset Data { get; }
    public string Descricao { get; }
    public OrigemLancamento Origem { get; }
    public string? ReversalOfId { get; }
    public DateTimeOffset CriadoEm { get; }

    public IReadOnlyList<PartidaContabil> Partidas => _partidas.AsReadOnly();

    public Money TotalDebito => Somar(NaturezaPartida.Debito);
    public Money TotalCredito => Somar(NaturezaPartida.Credito);

    /// <summary>Um lançamento gerado por <see cref="GerarEstorno"/> aponta para o original.</summary>
    public bool EhEstorno => ReversalOfId is not null;

    private LancamentoContabil(
        string id, string businessId, DateTimeOffset data, string descricao,
        OrigemLancamento origem, string? reversalOfId, DateTimeOffset criadoEm, IReadOnlyCollection<PartidaContabil> partidas)
    {
        Id = id;
        BusinessId = businessId;
        Data = data;
        Descricao = descricao;
        Origem = origem;
        ReversalOfId = reversalOfId;
        CriadoEm = criadoEm;
        _partidas.AddRange(partidas);
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static LancamentoContabil Reconstituir(
        string id, string businessId, DateTimeOffset data, string descricao, OrigemLancamento origem,
        string? reversalOfId, DateTimeOffset criadoEm, IReadOnlyCollection<PartidaContabil> partidas)
        => new(id, businessId, data, descricao, origem, reversalOfId, criadoEm, partidas);

    public static Result<LancamentoContabil> Criar(
        string businessId,
        DateTimeOffset data,
        string descricao,
        OrigemLancamento origem,
        IReadOnlyCollection<PartidaContabil> partidas,
        string? reversalOfId = null)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<LancamentoContabil>(new Error("financeiro.lancamento.business_id_obrigatorio", "businessId é obrigatório em todo lançamento contábil."));

        if (partidas.Count < 2 || partidas.All(p => p.Natureza == NaturezaPartida.Debito) || partidas.All(p => p.Natureza == NaturezaPartida.Credito))
            return Result.Falhar<LancamentoContabil>(new Error(
                "financeiro.lancamento.partidas_insuficientes",
                "Um lançamento contábil precisa de ao menos uma partida a débito e uma a crédito."));

        if (partidas.Any(p => !p.Valor.EhPositivo))
            return Result.Falhar<LancamentoContabil>(new Error(
                "financeiro.lancamento.partida_valor_invalido",
                "Toda partida contábil deve ter valor positivo — o lado (débito/crédito) é que expressa o sentido."));

        var totalDebito = partidas.Where(p => p.Natureza == NaturezaPartida.Debito).Aggregate(Money.Zero, (acumulado, p) => acumulado + p.Valor);
        var totalCredito = partidas.Where(p => p.Natureza == NaturezaPartida.Credito).Aggregate(Money.Zero, (acumulado, p) => acumulado + p.Valor);

        if (totalDebito != totalCredito)
            return Result.Falhar<LancamentoContabil>(new Error(
                "financeiro.lancamento.desbalanceado",
                $"Partidas não batem: débito {totalDebito.Formatado()} != crédito {totalCredito.Formatado()}. " +
                "Isso é um bug no gerador de partidas (LancamentoContabilFactory), nunca um dado de entrada do usuário."));

        var lancamento = new LancamentoContabil(IdGenerator.NovoId(), businessId, data, descricao, origem, reversalOfId, DateTimeOffset.UtcNow, partidas);
        lancamento.Raise(new LancamentoContabilRegistrado(lancamento.Id, businessId, origem.Chave, totalDebito.Centavos));
        return Result.Ok(lancamento);
    }

    /// <summary>
    /// Gera o lançamento de ESTORNO: partidas espelhadas (débito↔crédito invertidos), datado na
    /// data do estorno (nunca retroage ao período original — docs/financeiro-datamodel.md §4.4.4).
    /// O original permanece intocado; isto é sempre um novo <see cref="LancamentoContabil"/>.
    /// Espelhar um conjunto balanceado produz, por construção, outro conjunto balanceado — a
    /// invariante de <see cref="Criar"/> nunca pode falhar aqui, mas passamos pelo mesmo portão
    /// mesmo assim (nenhum atalho que crie um LancamentoContabil fora de <see cref="Criar"/>).
    /// </summary>
    public Result<LancamentoContabil> GerarEstorno(DateTimeOffset dataEstorno, string motivo)
    {
        if (EhEstorno)
            return Result.Falhar<LancamentoContabil>(new Error(
                "financeiro.lancamento.estorno_de_estorno",
                "Este lançamento já é um estorno. Para desfazer um estorno, estorne o lançamento ORIGINAL de novo (a cadeia é rastreada por ReversalOfId), nunca estorne um estorno."));

        var partidasInvertidas = _partidas.Select(p => p.Inverter()).ToList();
        var origemEstorno = Origem with { TipoFato = $"estorno.{Origem.TipoFato}" };
        return Criar(BusinessId, dataEstorno, $"Estorno — {motivo} (ref. lançamento {Id})", origemEstorno, partidasInvertidas, reversalOfId: Id);
    }

    private Money Somar(NaturezaPartida natureza)
        => _partidas.Where(p => p.Natureza == natureza).Aggregate(Money.Zero, (acumulado, p) => acumulado + p.Valor);
}
