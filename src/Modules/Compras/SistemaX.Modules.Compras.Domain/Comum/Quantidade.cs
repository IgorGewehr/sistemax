using System.Globalization;

namespace SistemaX.Modules.Compras.Domain.Comum;

/// <summary>
/// Quantidade em MILÉSIMOS-INTEIROS — o "<c>Money</c> das quantidades" (mesmo espírito de
/// centavos-inteiros). Cópia de propósito da mesma VO do Estoque: cada módulo tem a sua (nenhum
/// módulo referencia o Domain de outro — mesmo padrão já adotado por
/// <c>SistemaX.Modules.Estoque.Domain.Comum.Quantidade</c>).
/// </summary>
public readonly record struct Quantidade(long Milesimos) : IComparable<Quantidade>
{
    public static readonly Quantidade Zero = new(0);

    public decimal EmDecimal => Milesimos / 1000m;

    /// <summary>Arredondamento bancário (MidpointRounding.ToEven) — mesmo critério do <c>Money</c>.</summary>
    public static Quantidade DeDecimal(decimal valor)
        => new((long)Math.Round(valor * 1000m, MidpointRounding.ToEven));

    public static Quantidade DeInteiro(int unidades) => new(unidades * 1000L);

    public bool EhPositiva => Milesimos > 0;
    public bool EhZero => Milesimos == 0;

    public static Quantidade operator +(Quantidade a, Quantidade b) => new(a.Milesimos + b.Milesimos);
    public static Quantidade operator -(Quantidade a, Quantidade b) => new(a.Milesimos - b.Milesimos);

    public int CompareTo(Quantidade outra) => Milesimos.CompareTo(outra.Milesimos);

    /// <summary>Formata em pt-BR — só para exibição, nunca para cálculo.</summary>
    public string Formatado() => EmDecimal.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
}
