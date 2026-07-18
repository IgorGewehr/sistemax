using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Tests.Tempo;

/// <summary>
/// Domínio de <see cref="ApontamentoDeTempo"/> — invariantes de <c>Criar</c> (design-pai §3.4).
/// Decisão travada do dono para esta fatia: só minutos, sem custo/hora.
/// </summary>
public sealed class ApontamentoDeTempoTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Data = new(2026, 7, 17, 14, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Criar_SemNenhumVinculo_Falha()
    {
        var resultado = ApontamentoDeTempo.Criar(Biz, 30, Data, "op-1", "Igor", Data);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.apontamento.sem_vinculo", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComMinutosZero_Falha()
    {
        var resultado = ApontamentoDeTempo.Criar(Biz, 0, Data, "op-1", "Igor", Data, clienteId: "cliente-1");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.apontamento.minutos_invalidos", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_ComApenasProjetoId_Sucesso()
    {
        var resultado = ApontamentoDeTempo.Criar(Biz, 30, Data, "op-1", "Igor", Data, projetoId: "projeto-1");
        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Criar_ComApenasOrdemServicoId_Sucesso()
    {
        var resultado = ApontamentoDeTempo.Criar(Biz, 30, Data, "op-1", "Igor", Data, ordemServicoId: "os-1");
        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void CustoCentavos_SemSnapshot_EhSempreNulo()
    {
        var apontamento = ApontamentoDeTempo.Criar(Biz, 30, Data, "op-1", "Igor", Data, clienteId: "cliente-1").Valor;
        Assert.Null(apontamento.CustoHoraCentavosSnapshot);
        Assert.Null(apontamento.CustoCentavos);
    }
}
