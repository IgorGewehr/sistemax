using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;

namespace SistemaX.Modules.Fiscal.Tests;

public class SituacaoTributariaIcmsTests
{
    [Fact]
    public void ParaCsosn_EmRegimeNormal_Falha()
    {
        var resultado = SituacaoTributariaIcms.ParaCsosn(RegimeTributario.LucroPresumido, "102");
        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.situacao.csosn_fora_do_simples", resultado.Erro.Codigo);
    }

    [Fact]
    public void ParaCst_EmSimplesNacionalPleno_Falha()
    {
        var resultado = SituacaoTributariaIcms.ParaCst(RegimeTributario.SimplesNacional, "00");
        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.situacao.cst_fora_do_normal", resultado.Erro.Codigo);
    }

    [Fact]
    public void ParaCsosn_EmSimplesNacional_Sucesso()
    {
        var resultado = SituacaoTributariaIcms.ParaCsosn(RegimeTributario.SimplesNacional, "102");
        Assert.True(resultado.Sucesso);
        Assert.True(resultado.Valor.EhCsosn);
        Assert.Equal("102", resultado.Valor.Codigo);
    }

    /// <summary>Fecha a correção #2 de docs/fiscal/arquitetura.md §2.1 — SimplesNacionalSublimite
    /// (CRT=2) usa CST (tabela B), NUNCA CSOSN.</summary>
    [Fact]
    public void ParaCst_EmSimplesNacionalSublimite_Sucesso()
    {
        var resultado = SituacaoTributariaIcms.ParaCst(RegimeTributario.SimplesNacionalSublimite, "00");
        Assert.True(resultado.Sucesso);
        Assert.False(resultado.Valor.EhCsosn);
    }

    [Fact]
    public void ParaCsosn_EmSimplesNacionalSublimite_Falha()
    {
        var resultado = SituacaoTributariaIcms.ParaCsosn(RegimeTributario.SimplesNacionalSublimite, "102");
        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.situacao.csosn_fora_do_simples", resultado.Erro.Codigo);
    }
}
