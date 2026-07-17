using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Eventos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

/// <summary>Obrigação da empresa para com terceiro — fornecedor, comissão, folha.</summary>
public sealed class ContaAPagar : ContaFinanceiraBase
{
    public override bool EhContaAPagar => true;

    public string? FornecedorId { get; }

    private ContaAPagar(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal,
        IReadOnlyCollection<Parcela> parcelas, string? fornecedorId, CorrenteDeReceita? corrente)
        : base(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, parcelas, corrente)
    {
        FornecedorId = fornecedorId;
    }

    private ContaAPagar(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, StatusFinanceiro status,
        DateTimeOffset criadoEm, IReadOnlyCollection<Parcela> parcelas, string? fornecedorId, CorrenteDeReceita? corrente)
        : base(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, status, criadoEm, parcelas, corrente)
    {
        FornecedorId = fornecedorId;
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static ContaAPagar Reconstituir(
        string id, string businessId, SourceRef sourceRef, string descricao, string categoriaId,
        string? centroDeCustoId, DateTimeOffset dataCompetencia, Money valorTotal, StatusFinanceiro status,
        DateTimeOffset criadoEm, IReadOnlyCollection<Parcela> parcelas, string? fornecedorId, CorrenteDeReceita? corrente = null)
        => new(id, businessId, sourceRef, descricao, categoriaId, centroDeCustoId, dataCompetencia, valorTotal, status, criadoEm, parcelas, fornecedorId, corrente);

    public static Result<ContaAPagar> Criar(
        string businessId,
        SourceRef sourceRef,
        string descricao,
        string categoriaId,
        DateTimeOffset dataCompetencia,
        Money valorTotal,
        IReadOnlyCollection<Parcela> parcelas,
        string? centroDeCustoId = null,
        string? fornecedorId = null,
        CorrenteDeReceita? corrente = null)
    {
        var validacao = ValidarParcelas(valorTotal, parcelas);
        if (validacao.Falha) return Result.Falhar<ContaAPagar>(validacao.Erro);

        var conta = new ContaAPagar(
            IdGenerator.NovoId(), businessId, sourceRef, descricao, categoriaId,
            centroDeCustoId, dataCompetencia, valorTotal, parcelas, fornecedorId, corrente);

        conta.Raise(new ContaCriada(conta.Id, businessId, "pagar", valorTotal.Centavos, sourceRef.Chave));
        return Result.Ok(conta);
    }
}
