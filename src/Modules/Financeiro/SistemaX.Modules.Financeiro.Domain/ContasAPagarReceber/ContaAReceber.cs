using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Eventos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

/// <summary>Obrigação de terceiro para com a empresa — venda, OS faturada, pedido pago.</summary>
public sealed class ContaAReceber : ContaFinanceiraBase
{
    public override bool EhContaAPagar => false;

    public string? ClienteId { get; }

    /// <summary>Técnico/profissional que faturou o fato de origem (P1-7,
    /// docs/financeiro/revisao-domain-fit-cnpj.md) — hoje só permite CONSULTAR "quem faturou esta
    /// conta" a partir do Financeiro; NÃO gera comissão sozinho (falta o cadastro de percentual por
    /// tenant — quando ele existir, o handler de origem ganha a criação condicional de
    /// <c>ContaAPagar</c> categoria Comissões). NULLABLE — aditivo, a maioria das origens (venda,
    /// pedido, assinatura) não tem um profissional individual associado.</summary>
    public string? TecnicoId { get; }

    /// <summary>Repartição opcional do <see cref="ContaFinanceiraBase.ValorTotal"/> em mão de obra
    /// vs peças aplicadas (P1-7) — usada só para granularidade de relatório (ex.: "receita de mão
    /// de obra" separada de "receita de peças" dentro da corrente Servico); a cobrança/parcela
    /// continua sendo sobre o TOTAL. NULLABLE — aditivo, só a origem "OS faturada" preenche hoje.
    /// Quando ambos vêm preenchidos, a soma deve bater com <see cref="ContaFinanceiraBase.ValorTotal"/>.</summary>
    public Money? ValorServico { get; }

    /// <summary>Ver <see cref="ValorServico"/>.</summary>
    public Money? ValorPecas { get; }

    private ContaAReceber(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal,
        IReadOnlyCollection<Parcela> parcelas, string? clienteId, CorrenteDeReceita? corrente,
        string? tecnicoId, Money? valorServico, Money? valorPecas, int? mesesDeReconhecimento, string? projetoId)
        : base(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, parcelas, corrente, mesesDeReconhecimento, projetoId)
    {
        ClienteId = clienteId;
        TecnicoId = tecnicoId;
        ValorServico = valorServico;
        ValorPecas = valorPecas;
    }

    private ContaAReceber(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, StatusFinanceiro status,
        DateTimeOffset criadoEm, IReadOnlyCollection<Parcela> parcelas, string? clienteId, CorrenteDeReceita? corrente,
        string? tecnicoId, Money? valorServico, Money? valorPecas, int? mesesDeReconhecimento, string? projetoId)
        : base(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, status, criadoEm, parcelas, corrente, mesesDeReconhecimento, projetoId)
    {
        ClienteId = clienteId;
        TecnicoId = tecnicoId;
        ValorServico = valorServico;
        ValorPecas = valorPecas;
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static ContaAReceber Reconstituir(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, StatusFinanceiro status,
        DateTimeOffset criadoEm, IReadOnlyCollection<Parcela> parcelas, string? clienteId, CorrenteDeReceita? corrente = null,
        string? tecnicoId = null, Money? valorServico = null, Money? valorPecas = null, int? mesesDeReconhecimento = null,
        string? projetoId = null)
        => new(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, status, criadoEm, parcelas, clienteId, corrente,
            tecnicoId, valorServico, valorPecas, mesesDeReconhecimento, projetoId);

    public static Result<ContaAReceber> Criar(
        string businessId,
        SourceRef sourceRef,
        string descricao,
        string categoriaId,
        DateTimeOffset dataCompetencia,
        Money valorTotal,
        IReadOnlyCollection<Parcela> parcelas,
        string? centroDeCustoId = null,
        string? clienteId = null,
        CorrenteDeReceita? corrente = null,
        string? tecnicoId = null,
        Money? valorServico = null,
        Money? valorPecas = null,
        int? mesesDeReconhecimento = null,
        string? projetoId = null)
    {
        var validacao = ValidarParcelas(valorTotal, parcelas);
        if (validacao.Falha) return Result.Falhar<ContaAReceber>(validacao.Erro);

        if (valorServico is { } servico && valorPecas is { } pecas && servico + pecas != valorTotal)
            return Result.Falhar<ContaAReceber>(new Error(
                "financeiro.conta.repartição_servico_pecas_nao_bate",
                $"Soma de mão de obra ({servico.Formatado()}) e peças ({pecas.Formatado()}) é diferente do valor total da conta ({valorTotal.Formatado()})."));

        if (mesesDeReconhecimento is { } meses && meses < 1)
            return Result.Falhar<ContaAReceber>(new Error(
                "financeiro.conta.meses_de_reconhecimento_invalido",
                "Reconhecimento diferido precisa de ao menos 1 competência."));

        var conta = new ContaAReceber(
            IdGenerator.NovoId(), businessId, sourceRef, descricao, categoriaId,
            centroDeCustoId, dataCompetencia, valorTotal, parcelas, clienteId, corrente,
            tecnicoId, valorServico, valorPecas, mesesDeReconhecimento, projetoId);

        conta.Raise(new ContaCriada(conta.Id, businessId, "receber", valorTotal.Centavos, sourceRef.Chave));
        return Result.Ok(conta);
    }
}
