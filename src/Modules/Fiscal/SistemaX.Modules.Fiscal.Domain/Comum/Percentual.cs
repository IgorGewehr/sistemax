using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Domain.Comum;

/// <summary>
/// O "<c>Money</c> das alíquotas" — nunca <c>decimal</c> cru carregando alíquota/redução/MVA por
/// várias camadas (R1 do CLAUDE.md, aplicado a percentuais). Fixo em MILIONÉSIMOS (6 casas
/// decimais — cobre MVA/ICMS-ST sem o arredondamento de ponto flutuante que motivou este tipo).
/// </summary>
public readonly record struct Percentual(long Milionesimos)
{
    public static readonly Percentual Zero = new(0);

    public decimal EmFracao => Milionesimos / 1_000_000m;

    public static Percentual DePorcentagem(decimal percentual) =>
        new((long)Math.Round(percentual / 100m * 1_000_000m, MidpointRounding.ToEven));

    /// <summary>Aplica a alíquota sobre uma base monetária — único ponto do módulo onde uma
    /// alíquota vira <see cref="Money"/>, com o MESMO critério de arredondamento bancário de
    /// <c>Money.DeReais</c>.</summary>
    public Money AplicarSobre(Money base_) =>
        Money.DeReais(Math.Round(base_.EmReais * EmFracao, 2, MidpointRounding.ToEven));
}
