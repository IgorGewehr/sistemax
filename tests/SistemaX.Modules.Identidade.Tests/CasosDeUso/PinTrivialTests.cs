using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Tests.CasosDeUso;

public sealed class PinTrivialTests
{
    [Theory]
    [InlineData("0000")]
    [InlineData("1111")]
    [InlineData("9999")]
    [InlineData("1234")]
    [InlineData("2345")]
    [InlineData("4321")]
    [InlineData("0123")]
    [InlineData("012")]
    [InlineData("")]
    public void PinsFaceis_SaoTriviais(string pin)
    {
        Assert.True(PinTrivial.EhTrivial(pin));
    }

    [Theory]
    [InlineData("7391")]
    [InlineData("1357")]
    [InlineData("2846")]
    [InlineData("1243")]
    [InlineData("90210")]
    public void PinsSemPadraoObvio_NaoSaoTriviais(string pin)
    {
        Assert.False(PinTrivial.EhTrivial(pin));
    }
}
