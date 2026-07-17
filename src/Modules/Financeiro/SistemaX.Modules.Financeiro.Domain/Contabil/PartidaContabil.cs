using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Domain.Contabil;

/// <summary>
/// Linha filha de um <see cref="LancamentoContabil"/>: uma perna débito ou crédito contra uma
/// conta do plano de contas. NUNCA é digitada por humano — é gerada por
/// <see cref="LancamentoContabilFactory"/> a partir de um fato de negócio single-entry.
/// </summary>
public sealed record PartidaContabil(string ContaContabilId, NaturezaPartida Natureza, Money Valor)
{
    public static PartidaContabil Debito(string contaContabilId, Money valor) => new(contaContabilId, NaturezaPartida.Debito, valor);

    public static PartidaContabil Credito(string contaContabilId, Money valor) => new(contaContabilId, NaturezaPartida.Credito, valor);

    /// <summary>Espelha a partida para geração de estorno: débito vira crédito e vice-versa, mesmo valor.</summary>
    public PartidaContabil Inverter() => this with
    {
        Natureza = Natureza == NaturezaPartida.Debito ? NaturezaPartida.Credito : NaturezaPartida.Debito
    };
}
