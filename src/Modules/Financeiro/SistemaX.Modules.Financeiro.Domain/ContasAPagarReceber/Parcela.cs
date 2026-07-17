using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.Fsm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;

/// <summary>
/// Fatia agendada de uma <c>ContaAPagar</c>/<c>ContaAReceber</c> — parcelamento nativo (1 conta,
/// N parcelas). Entidade FILHA do agregado (nunca uma raiz própria): toda mutação passa pelo
/// aggregate root (<c>ContaFinanceiraBase</c>), nunca é chamada solta de fora do módulo — por
/// isso os métodos de mutação são <c>internal</c>.
/// </summary>
public sealed class Parcela : Entity<string>
{
    public int Numero { get; }
    public DateTimeOffset Vencimento { get; private set; }
    public Money Valor { get; }
    public Money ValorPago { get; private set; }
    public StatusFinanceiro Status { get; private set; }
    public DateTimeOffset? DataLiquidacao { get; private set; }
    public string? FormaPagamentoId { get; private set; }

    private Parcela(string id, int numero, DateTimeOffset vencimento, Money valor)
    {
        Id = id;
        Numero = numero;
        Vencimento = vencimento;
        Valor = valor;
        ValorPago = Money.Zero;
        Status = StatusFinanceiro.Aberto;
    }

    public static Parcela Criar(int numero, DateTimeOffset vencimento, Money valor) => new(IdGenerator.NovoId(), numero, vencimento, valor);

    /// <summary>REIDRATAÇÃO a partir do banco — chamada pelo repositório ao montar a lista de parcelas de uma conta. Não valida, não levanta evento.</summary>
    public static Parcela Reconstituir(
        string id, int numero, DateTimeOffset vencimento, Money valor, Money valorPago,
        StatusFinanceiro status, DateTimeOffset? dataLiquidacao, string? formaPagamentoId)
        => new(id, numero, vencimento, valor)
        {
            ValorPago = valorPago,
            Status = status,
            DataLiquidacao = dataLiquidacao,
            FormaPagamentoId = formaPagamentoId
        };

    public bool TemPagamentoRegistrado => ValorPago.EhPositivo;

    /// <summary>Registra liquidação total ou parcial. Chamado só pelo aggregate root.</summary>
    internal Result RegistrarPagamento(Money valorPago, DateTimeOffset dataPagamento, string formaPagamentoId)
    {
        if (!valorPago.EhPositivo)
            return Result.Falhar(new Error("financeiro.parcela.valor_pagamento_invalido", "Valor pago deve ser positivo."));

        var novoTotalPago = ValorPago + valorPago;
        if (novoTotalPago.Centavos > Valor.Centavos)
            return Result.Falhar(new Error(
                "financeiro.parcela.pagamento_excede_valor",
                $"Pagamento de {valorPago.Formatado()} faria o total pago ({novoTotalPago.Formatado()}) exceder o valor da parcela ({Valor.Formatado()})."));

        var novoStatus = novoTotalPago == Valor ? StatusFinanceiro.Pago : StatusFinanceiro.Parcial;
        var transicao = StatusFinanceiroFsm.AssertirTransicao(Status, novoStatus);
        if (transicao.Falha) return transicao;

        ValorPago = novoTotalPago;
        Status = novoStatus;
        DataLiquidacao = dataPagamento;
        FormaPagamentoId = formaPagamentoId;
        return Result.Ok();
    }

    internal Result MarcarAtrasada(DateTimeOffset referencia)
    {
        if (Status is not (StatusFinanceiro.Aberto or StatusFinanceiro.Parcial)) return Result.Ok(); // idempotente: não é erro reavaliar
        if (referencia <= Vencimento) return Result.Ok();

        var transicao = StatusFinanceiroFsm.AssertirTransicao(Status, StatusFinanceiro.Atrasado);
        if (transicao.Falha) return transicao;

        Status = StatusFinanceiro.Atrasado;
        return Result.Ok();
    }

    internal Result Cancelar()
    {
        if (TemPagamentoRegistrado)
            return Result.Falhar(new Error("financeiro.parcela.cancelar_com_pagamento", "Parcela com pagamento registrado não pode ser cancelada — lance um estorno."));

        var transicao = StatusFinanceiroFsm.AssertirTransicao(Status, StatusFinanceiro.Cancelado);
        if (transicao.Falha) return transicao;

        Status = StatusFinanceiro.Cancelado;
        return Result.Ok();
    }
}
