using System.Text.Json.Serialization;

namespace SistemaX.SharedKernel;

/// <summary>
/// Valor monetário em CENTAVOS-INTEIROS. Regra dura do projeto: dinheiro NUNCA é double/float.
/// Todo dinheiro no sistema — preço, custo, saldo, total, imposto — é <see cref="Money"/>.
/// Guardar como <c>long</c> de centavos elimina erro de arredondamento de ponto flutuante,
/// que é inaceitável num sistema cujo coração é o financeiro.
///
/// Contrato de wire (ver plano de produção §2.5 — Bridge HTTP local/F1a): serializado como
/// <c>{ centavos, moeda }</c> e SÓ isso — as propriedades computadas abaixo levam
/// <see cref="JsonIgnoreAttribute"/> pra não vazarem no JSON (proibido float de reais no wire).
/// </summary>
public readonly record struct Money(long Centavos, string Moeda = "BRL")
{
    public static readonly Money Zero = new(0);

    [JsonIgnore]
    public decimal EmReais => Centavos / 100m;

    /// <summary>Cria a partir de reais decimais (arredondamento bancário — MidpointRounding.ToEven).</summary>
    public static Money DeReais(decimal reais, string moeda = "BRL")
        => new((long)Math.Round(reais * 100m, MidpointRounding.ToEven), moeda);

    [JsonIgnore]
    public bool EhPositivo => Centavos > 0;

    [JsonIgnore]
    public bool EhNegativo => Centavos < 0;

    [JsonIgnore]
    public bool EhZero => Centavos == 0;

    public static Money operator +(Money a, Money b) { GarantirMesmaMoeda(a, b); return new(a.Centavos + b.Centavos, a.Moeda); }
    public static Money operator -(Money a, Money b) { GarantirMesmaMoeda(a, b); return new(a.Centavos - b.Centavos, a.Moeda); }
    public static Money operator *(Money a, int fator) => new(a.Centavos * fator, a.Moeda);
    public static Money operator -(Money a) => new(-a.Centavos, a.Moeda);

    /// <summary>Formata em pt-BR (ex.: R$ 1.234,56). Só para exibição — nunca para cálculo.</summary>
    public string Formatado()
        => EmReais.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));

    private static void GarantirMesmaMoeda(Money a, Money b)
    {
        if (a.Moeda != b.Moeda)
            throw new InvalidOperationException($"Operação monetária entre moedas diferentes: {a.Moeda} e {b.Moeda}.");
    }
}
