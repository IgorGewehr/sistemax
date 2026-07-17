using SistemaX.Modules.Fiscal.Domain.Regimes;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>R8 do CLAUDE.md: toda FSM/invariante crítica tem teste. Aqui a invariante é
/// tributária, não de status — mas é igualmente crítica (a correção #2 de
/// docs/fiscal/arquitetura.md §2.1 existe exatamente para não regredir este comportamento).</summary>
public class RegimeTributarioTests
{
    [Theory]
    [InlineData(RegimeTributario.Mei, true)]
    [InlineData(RegimeTributario.SimplesNacional, true)]
    [InlineData(RegimeTributario.SimplesNacionalSublimite, false)]
    [InlineData(RegimeTributario.LucroPresumido, false)]
    [InlineData(RegimeTributario.LucroReal, false)]
    public void UsaCsosn_SoRegimesPlenosDoSimplesUsamCsosn(RegimeTributario regime, bool esperado)
        => Assert.Equal(esperado, regime.UsaCsosn());

    [Theory]
    [InlineData(RegimeTributario.Mei, "1")]
    [InlineData(RegimeTributario.SimplesNacional, "1")]
    [InlineData(RegimeTributario.SimplesNacionalSublimite, "2")]
    [InlineData(RegimeTributario.LucroPresumido, "3")]
    [InlineData(RegimeTributario.LucroReal, "3")]
    public void Crt_MapeiaFatoFechadoDoLayoutSefaz(RegimeTributario regime, string crtEsperado)
        => Assert.Equal(crtEsperado, regime.Crt());
}
