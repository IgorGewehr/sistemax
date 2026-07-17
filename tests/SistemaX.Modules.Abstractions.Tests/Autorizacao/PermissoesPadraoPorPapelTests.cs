using SistemaX.Modules.Abstractions.Autorizacao;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Prova que o mapa <see cref="PermissoesPadraoPorPapel"/> espelha, campo a campo, o
/// <c>PAPEL_PERMISSOES_PADRAO</c> de <c>web/src/lib/permissions.ts</c> — se um dos dois lados
/// divergir num PR futuro, é este teste que quebra primeiro.
/// </summary>
public sealed class PermissoesPadraoPorPapelTests
{
    [Theory]
    [InlineData(Papel.Founder)]
    [InlineData(Papel.Admin)]
    public void FounderEAdmin_TemTodasAsPermissoes(Papel papel)
    {
        foreach (var permissao in ModuloAcoes.Todas())
        {
            Assert.True(
                PermissoesPadraoPorPapel.Tem(papel, permissao.Modulo, permissao.Acao),
                $"{papel} deveria ter {permissao}.");
        }
    }

    [Theory]
    [InlineData(Modulo.Vendas, Acao.Ver, true)]
    [InlineData(Modulo.Vendas, Acao.Editar, true)]
    [InlineData(Modulo.Financeiro, Acao.Ver, true)]
    [InlineData(Modulo.Financeiro, Acao.Editar, false)]
    [InlineData(Modulo.Fiscal, Acao.Ver, true)]
    [InlineData(Modulo.Fiscal, Acao.EmitirFiscal, false)]
    [InlineData(Modulo.Configuracoes, Acao.GerenciarUsuarios, false)]
    public void Manager_TemSoAsPermissoesDoMapaDoFront(Modulo modulo, Acao acao, bool esperado)
    {
        Assert.Equal(esperado, PermissoesPadraoPorPapel.Tem(Papel.Manager, modulo, acao));
    }

    [Theory]
    [InlineData(Modulo.Vendas, Acao.Editar, true)]
    [InlineData(Modulo.Estoque, Acao.Ver, true)]
    [InlineData(Modulo.Estoque, Acao.Editar, false)]
    [InlineData(Modulo.Financeiro, Acao.Ver, false)]
    [InlineData(Modulo.Compras, Acao.Ver, false)]
    [InlineData(Modulo.Configuracoes, Acao.Ver, false)]
    public void Operator_TemSoAsPermissoesDoMapaDoFront(Modulo modulo, Acao acao, bool esperado)
    {
        Assert.Equal(esperado, PermissoesPadraoPorPapel.Tem(Papel.Operator, modulo, acao));
    }

    [Theory]
    [InlineData(Modulo.Vendas, Acao.Ver, true)]
    [InlineData(Modulo.Vendas, Acao.Editar, false)]
    [InlineData(Modulo.Estoque, Acao.Ver, true)]
    [InlineData(Modulo.Estoque, Acao.Editar, false)]
    [InlineData(Modulo.Pdv, Acao.Ver, false)]
    [InlineData(Modulo.Financeiro, Acao.Ver, false)]
    public void Viewer_TemSoAsPermissoesDoMapaDoFront(Modulo modulo, Acao acao, bool esperado)
    {
        Assert.Equal(esperado, PermissoesPadraoPorPapel.Tem(Papel.Viewer, modulo, acao));
    }

    [Fact]
    public void Hierarquia_EspelhaAConvencaoDoEcossistema()
    {
        Assert.Equal(100, PapelHierarquia.Valores[Papel.Founder]);
        Assert.Equal(80, PapelHierarquia.Valores[Papel.Admin]);
        Assert.Equal(60, PapelHierarquia.Valores[Papel.Manager]);
        Assert.Equal(40, PapelHierarquia.Valores[Papel.Operator]);
        Assert.Equal(20, PapelHierarquia.Valores[Papel.Viewer]);
    }

    [Theory]
    [InlineData(Papel.Founder, true)]
    [InlineData(Papel.Admin, true)]
    [InlineData(Papel.Manager, false)]
    [InlineData(Papel.Operator, false)]
    [InlineData(Papel.Viewer, false)]
    public void PodeAdministrarUsuarios_SoFounderEAdmin(Papel papel, bool esperado)
    {
        Assert.Equal(esperado, PapelHierarquia.PodeAdministrarUsuarios(papel));
    }
}
