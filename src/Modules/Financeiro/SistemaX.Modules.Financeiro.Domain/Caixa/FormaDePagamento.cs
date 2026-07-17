using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Caixa;

/// <summary>Categoria de meio de pagamento — usada para decidir prazo de compensação e taxa.</summary>
public enum TipoFormaPagamento
{
    Dinheiro,
    Pix,
    Debito,
    Credito,
    Boleto,
    Transferencia,
    Outro
}

/// <summary>
/// Como o dinheiro se move — carrega os metadados de liquidação (taxa de cartão, prazo de
/// compensação D+0/D+30) usados para projetar <c>dataMovimento</c> vs data da operação
/// (docs/financeiro-datamodel.md §2.2).
///
/// LAR ÚNICO do MDR/lag por forma de pagamento: <c>FatoRecebiveisProjection</c> resolve
/// <see cref="TaxaPercentual"/>/<see cref="PrazoCompensacaoDias"/> consultando
/// <c>IFormaDePagamentoRepository.ObterPorNomeAsync</c> — nunca uma tabela/config paralela
/// (a antiga <c>ConfiguracaoDeRecebiveisOptions</c>, removida nesta reconciliação).
/// </summary>
public sealed class FormaDePagamento : Entity<string>
{
    public string BusinessId { get; private set; } = string.Empty;
    public string Nome { get; private set; } = string.Empty;
    public TipoFormaPagamento Tipo { get; private set; }

    /// <summary>Taxa percentual sobre o valor bruto (ex.: 0.0349m = 3,49% de maquininha de crédito).</summary>
    public decimal TaxaPercentual { get; private set; }

    /// <summary>Dias até o dinheiro efetivamente compensar (D+0 para PIX/dinheiro, D+30 típico de crédito).</summary>
    public int PrazoCompensacaoDias { get; private set; }

    /// <summary>Id da <see cref="ContaBancaria"/> onde o valor liquidado cai — opcional (nem toda
    /// instalação já mapeou "essa maquininha cai em qual conta").</summary>
    public string? ContaLiquidacaoId { get; private set; }

    public bool Ativo { get; private set; }

    private FormaDePagamento()
    {
    }

    public static Result<FormaDePagamento> Criar(
        string businessId, string nome, TipoFormaPagamento tipo, decimal taxaPercentual = 0m,
        int prazoCompensacaoDias = 0, string? contaLiquidacaoId = null)
    {
        if (string.IsNullOrWhiteSpace(businessId))
            return Result.Falhar<FormaDePagamento>(new Error("financeiro.forma_pagamento.business_invalido", "BusinessId é obrigatório."));
        if (string.IsNullOrWhiteSpace(nome))
            return Result.Falhar<FormaDePagamento>(new Error("financeiro.forma_pagamento.nome_invalido", "Nome da forma de pagamento é obrigatório."));
        if (taxaPercentual is < 0 or > 1)
            return Result.Falhar<FormaDePagamento>(new Error("financeiro.forma_pagamento.taxa_invalida", "Taxa percentual deve estar entre 0 e 1 (fração, não %)."));
        if (prazoCompensacaoDias < 0)
            return Result.Falhar<FormaDePagamento>(new Error("financeiro.forma_pagamento.prazo_invalido", "Prazo de compensação não pode ser negativo."));

        return Result.Ok(new FormaDePagamento
        {
            Id = IdGenerator.NovoId(),
            BusinessId = businessId,
            Nome = nome,
            Tipo = tipo,
            TaxaPercentual = taxaPercentual,
            PrazoCompensacaoDias = prazoCompensacaoDias,
            ContaLiquidacaoId = string.IsNullOrWhiteSpace(contaLiquidacaoId) ? null : contaLiquidacaoId,
            Ativo = true
        });
    }

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento.</summary>
    public static FormaDePagamento Reconstituir(
        string id, string businessId, string nome, TipoFormaPagamento tipo, decimal taxaPercentual,
        int prazoCompensacaoDias, string? contaLiquidacaoId, bool ativo)
        => new()
        {
            Id = id,
            BusinessId = businessId,
            Nome = nome,
            Tipo = tipo,
            TaxaPercentual = taxaPercentual,
            PrazoCompensacaoDias = prazoCompensacaoDias,
            ContaLiquidacaoId = contaLiquidacaoId,
            Ativo = ativo
        };

    public Result Inativar()
    {
        if (!Ativo)
            return Result.Falhar(new Error("financeiro.forma_pagamento.ja_inativa", "Forma de pagamento já está inativa."));

        Ativo = false;
        return Result.Ok();
    }

    public Result Reativar()
    {
        if (Ativo)
            return Result.Falhar(new Error("financeiro.forma_pagamento.ja_ativa", "Forma de pagamento já está ativa."));

        Ativo = true;
        return Result.Ok();
    }

    public DateTimeOffset CalcularDataCompensacao(DateTimeOffset dataOperacao) => dataOperacao.AddDays(PrazoCompensacaoDias);

    public Money CalcularTaxa(Money valorBruto) => Money.DeReais(valorBruto.EmReais * TaxaPercentual);

    public Money CalcularValorLiquido(Money valorBruto) => valorBruto - CalcularTaxa(valorBruto);
}
