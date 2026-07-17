using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Domain.Saldos;

/// <summary>
/// Read-model PERSISTIDO (produto × depósito) — cache materializado do razão, atualizado na MESMA
/// transação que grava o <see cref="MovimentoDeEstoque"/>. Nunca é fonte de verdade: é sempre
/// recomputável do zero por replay (ver <c>RecalcularSaldoUseCase</c>, Application). Divergência
/// entre este cache e o razão é sempre bug DO CACHE.
/// </summary>
public sealed class SaldoDeItem
{
    public string TenantId { get; private set; }
    public string ProdutoId { get; private set; }
    public string DepositoId { get; private set; }
    public Quantidade Fisico { get; private set; }
    public Quantidade Reservado { get; private set; }
    public Money CustoMedio { get; private set; }
    public string? UltimoMovimentoId { get; private set; }

    /// <summary>O que o PDV consulta ("posso vender?") e o que a OS reserva contra. Pode ficar
    /// NEGATIVO — sinal de "hora de inventariar", nunca um estado proibido.</summary>
    public Quantidade Disponivel => Fisico - Reservado;

    /// <summary>Físico × custo médio, em centavos-inteiros (nunca via <c>double</c>).</summary>
    public Money ValorTotal
    {
        get
        {
            var centavos = (decimal)Fisico.Milesimos * CustoMedio.Centavos / 1000m;
            return new Money((long)Math.Round(centavos, MidpointRounding.ToEven));
        }
    }

    private SaldoDeItem(string tenantId, string produtoId, string depositoId, Quantidade fisico, Quantidade reservado, Money custoMedio, string? ultimoMovimentoId)
    {
        TenantId = tenantId;
        ProdutoId = produtoId;
        DepositoId = depositoId;
        Fisico = fisico;
        Reservado = reservado;
        CustoMedio = custoMedio;
        UltimoMovimentoId = ultimoMovimentoId;
    }

    public static SaldoDeItem Vazio(string tenantId, string produtoId, string depositoId)
        => new(tenantId, produtoId, depositoId, Quantidade.Zero, Quantidade.Zero, Money.Zero, ultimoMovimentoId: null);

    /// <summary>REIDRATAÇÃO a partir do banco — não valida, não levanta evento (R6).</summary>
    public static SaldoDeItem Reconstituir(string tenantId, string produtoId, string depositoId, Quantidade fisico, Quantidade reservado, Money custoMedio, string? ultimoMovimentoId)
        => new(tenantId, produtoId, depositoId, fisico, reservado, custoMedio, ultimoMovimentoId);

    /// <summary>
    /// Aplica o efeito de um movimento já gravado no razão. A ORDEM importa: em <c>Entrada</c>, o
    /// custo médio é recalculado usando o <see cref="Fisico"/> ANTERIOR à aplicação — só então o
    /// delta físico/reservado é somado. Demais tipos não tocam <see cref="CustoMedio"/> (Saída/
    /// Perda apenas congelam o CM vigente no <c>CustoUnitario</c> do próprio movimento).
    /// </summary>
    public void AplicarMovimento(MovimentoDeEstoque movimento)
    {
        if (movimento.Tipo == TipoMovimento.Entrada)
            CustoMedio = CalculadoraDeCustoMedio.Recalcular(Fisico, CustoMedio, movimento.Quantidade, movimento.CustoUnitario);

        Fisico += movimento.EfeitoFisico;
        Reservado += movimento.EfeitoReservado;
        UltimoMovimentoId = movimento.Id;
    }
}
