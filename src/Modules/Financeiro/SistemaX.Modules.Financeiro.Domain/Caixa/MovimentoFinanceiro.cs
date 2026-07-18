using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Eventos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>
/// O fato de CAIXA: dinheiro que de fato mudou de mão. Nasce da liquidação (total ou parcial) de
/// uma <c>Parcela</c>, ou de um recebimento à vista sem conta a receber prévia — mas SEMPRE com
/// <see cref="ParcelaId"/> preenchido, mesmo que a parcela tenha sido criada e liquidada no mesmo
/// instante (venda à vista). Isso preserva a regra "toda entrada de caixa tem uma origem de
/// competência rastreável" (docs/financeiro-datamodel.md §3).
///
/// IMUTÁVEL após criado: um <c>MovimentoFinanceiro</c> registrado nunca é editado/apagado.
/// Corrigir é sempre <see cref="GerarEstorno"/> — um novo movimento de sinal invertido, com
/// <see cref="ReversalOfId"/> apontando para o original, datado na data do estorno (nunca
/// retroage ao período original — docs/financeiro-datamodel.md §4.4).
/// </summary>
public sealed class MovimentoFinanceiro : AggregateRoot<string>
{
    public string BusinessId { get; }
    public string ContaBancariaCaixaId { get; }
    public string FormaPagamentoId { get; }
    public string ParcelaId { get; }
    public string ContaOrigemId { get; }
    public TipoMovimentoFinanceiro Tipo { get; }
    public Money Valor { get; }
    public DateTimeOffset DataMovimento { get; }
    public SourceRef Origem { get; }
    public string? ReversalOfId { get; }
    public DateTimeOffset CriadoEm { get; }

    /// <summary>Dimensão "corrente de receita" (P0-1) — mesma semântica nullable/aditiva de
    /// <see cref="Domain.ContasAPagarReceber.ContaFinanceiraBase.Corrente"/>. Um estorno sempre
    /// herda a corrente do movimento original (<see cref="GerarEstorno"/>) — reverter um fato não
    /// muda a que corrente ele pertence.</summary>
    public CorrenteDeReceita? Corrente { get; }

    /// <summary>Dimensão "Projeto" (docs/financeiro/design-analise-por-projeto.md §3.2) — mesma
    /// semântica nullable/aditiva de <see cref="Corrente"/>. Um estorno sempre herda o projeto do
    /// movimento original (<see cref="GerarEstorno"/>), mesmo racional de <see cref="Corrente"/>:
    /// reverter um fato não muda a que projeto ele pertence.</summary>
    public string? ProjetoId { get; }

    public bool EhEstorno => ReversalOfId is not null;

    private MovimentoFinanceiro(
        string id, string businessId, string contaBancariaCaixaId, string formaPagamentoId,
        string parcelaId, string contaOrigemId, TipoMovimentoFinanceiro tipo, Money valor,
        DateTimeOffset dataMovimento, SourceRef origem, string? reversalOfId, DateTimeOffset criadoEm,
        CorrenteDeReceita? corrente, string? projetoId)
    {
        Id = id;
        BusinessId = businessId;
        ContaBancariaCaixaId = contaBancariaCaixaId;
        FormaPagamentoId = formaPagamentoId;
        ParcelaId = parcelaId;
        ContaOrigemId = contaOrigemId;
        Tipo = tipo;
        Valor = valor;
        DataMovimento = dataMovimento;
        Origem = origem;
        ReversalOfId = reversalOfId;
        CriadoEm = criadoEm;
        Corrente = corrente;
        ProjetoId = projetoId;
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static MovimentoFinanceiro Reconstituir(
        string id, string businessId, string contaBancariaCaixaId, string formaPagamentoId,
        string parcelaId, string contaOrigemId, TipoMovimentoFinanceiro tipo, Money valor,
        DateTimeOffset dataMovimento, SourceRef origem, string? reversalOfId, DateTimeOffset criadoEm,
        CorrenteDeReceita? corrente = null, string? projetoId = null)
        => new(id, businessId, contaBancariaCaixaId, formaPagamentoId, parcelaId, contaOrigemId, tipo, valor, dataMovimento, origem, reversalOfId, criadoEm, corrente, projetoId);

    public static Result<MovimentoFinanceiro> Registrar(
        string businessId, string contaBancariaCaixaId, string formaPagamentoId, string parcelaId,
        string contaOrigemId, TipoMovimentoFinanceiro tipo, Money valor, DateTimeOffset dataMovimento, SourceRef origem,
        CorrenteDeReceita? corrente = null, string? projetoId = null)
    {
        if (string.IsNullOrWhiteSpace(parcelaId))
            return Result.Falhar<MovimentoFinanceiro>(new Error(
                "financeiro.movimento.sem_parcela",
                "Todo MovimentoFinanceiro precisa referenciar uma Parcela de origem — não existe entrada/saída de caixa sem rastro de competência."));

        if (!valor.EhPositivo)
            return Result.Falhar<MovimentoFinanceiro>(new Error("financeiro.movimento.valor_invalido", "Valor do movimento deve ser positivo — o sentido é expresso por Tipo, não pelo sinal do valor."));

        var movimento = new MovimentoFinanceiro(
            IdGenerator.NovoId(), businessId, contaBancariaCaixaId, formaPagamentoId, parcelaId,
            contaOrigemId, tipo, valor, dataMovimento, origem, reversalOfId: null, DateTimeOffset.UtcNow, corrente, projetoId);

        movimento.Raise(new MovimentoFinanceiroRegistrado(movimento.Id, businessId, valor.Centavos, tipo == TipoMovimentoFinanceiro.Entrada));
        return Result.Ok(movimento);
    }

    /// <summary>
    /// Estorno = novo fato imutável de sinal invertido (Entrada↔Saída), nunca edição do original.
    /// </summary>
    public Result<MovimentoFinanceiro> GerarEstorno(DateTimeOffset dataEstorno, SourceRef origemEstorno)
    {
        if (EhEstorno)
            return Result.Falhar<MovimentoFinanceiro>(new Error(
                "financeiro.movimento.estorno_de_estorno",
                "Este movimento já é um estorno. Estorne o movimento ORIGINAL novamente se necessário — nunca encadeie estorno de estorno."));

        var tipoInvertido = Tipo == TipoMovimentoFinanceiro.Entrada ? TipoMovimentoFinanceiro.Saida : TipoMovimentoFinanceiro.Entrada;

        var estorno = new MovimentoFinanceiro(
            IdGenerator.NovoId(), BusinessId, ContaBancariaCaixaId, FormaPagamentoId, ParcelaId,
            ContaOrigemId, tipoInvertido, Valor, dataEstorno, origemEstorno, reversalOfId: Id, DateTimeOffset.UtcNow, Corrente, ProjetoId);

        estorno.Raise(new MovimentoFinanceiroEstornado(estorno.Id, Id, Valor.Centavos));
        return Result.Ok(estorno);
    }
}
