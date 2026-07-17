using System.Globalization;

namespace SistemaX.Modules.Fiscal.Domain.Comum;

/// <summary>
/// Quantidade em MILÉSIMOS-INTEIROS — o "<c>Money</c> das quantidades", mesma cópia de propósito
/// de <c>SistemaX.Modules.Estoque.Domain.Comum.Quantidade</c> (regra de fronteira do repo: Fiscal
/// nunca referencia Estoque.Domain). 1 UN = 1_000; 0,250 KG = 250.
/// </summary>
public readonly record struct Quantidade(long Milesimos) : IComparable<Quantidade>
{
    public static readonly Quantidade Zero = new(0);

    public decimal EmDecimal => Milesimos / 1000m;

    public static Quantidade DeDecimal(decimal valor)
        => new((long)Math.Round(valor * 1000m, MidpointRounding.ToEven));

    public static Quantidade DeInteiro(int unidades) => new(unidades * 1000L);

    public bool EhPositiva => Milesimos > 0;
    public bool EhNegativa => Milesimos < 0;
    public bool EhZero => Milesimos == 0;

    public static Quantidade operator +(Quantidade a, Quantidade b) => new(a.Milesimos + b.Milesimos);
    public static Quantidade operator -(Quantidade a, Quantidade b) => new(a.Milesimos - b.Milesimos);
    public static Quantidade operator -(Quantidade a) => new(-a.Milesimos);

    public static bool operator >(Quantidade a, Quantidade b) => a.Milesimos > b.Milesimos;
    public static bool operator <(Quantidade a, Quantidade b) => a.Milesimos < b.Milesimos;
    public static bool operator >=(Quantidade a, Quantidade b) => a.Milesimos >= b.Milesimos;
    public static bool operator <=(Quantidade a, Quantidade b) => a.Milesimos <= b.Milesimos;

    public int CompareTo(Quantidade outra) => Milesimos.CompareTo(outra.Milesimos);

    /// <summary>Formata em pt-BR — só para exibição, nunca para cálculo.</summary>
    public string Formatado() => EmDecimal.ToString("0.###", CultureInfo.GetCultureInfo("pt-BR"));
}
