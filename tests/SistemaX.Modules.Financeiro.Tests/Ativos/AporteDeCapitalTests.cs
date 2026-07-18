using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>Invariantes de <see cref="AporteDeCapital"/> (docs/financeiro/design-imobilizado-roi.md
/// §3.3) — registro leve, sem FSM, sem lançamento contábil.</summary>
public sealed class AporteDeCapitalTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Criar_com_dados_validos_retorna_sucesso()
    {
        var resultado = AporteDeCapital.Criar(Biz, Money.DeReais(20_000), new DateOnly(2026, 7, 1), "Capital de giro inicial", Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2_000_000, resultado.Valor.Valor.Centavos);
        Assert.Equal("Capital de giro inicial", resultado.Valor.Descricao);
    }

    [Fact]
    public void Criar_com_valor_zero_ou_negativo_falha()
    {
        var zero = AporteDeCapital.Criar(Biz, Money.Zero, new DateOnly(2026, 7, 1), "Aporte", Agora);
        var negativo = AporteDeCapital.Criar(Biz, new Money(-100), new DateOnly(2026, 7, 1), "Aporte", Agora);

        Assert.True(zero.Falha);
        Assert.True(negativo.Falha);
        Assert.Equal("financeiro.aporte.valor_invalido", zero.Erro.Codigo);
    }

    [Fact]
    public void Criar_sem_descricao_falha()
    {
        var resultado = AporteDeCapital.Criar(Biz, Money.DeReais(1_000), new DateOnly(2026, 7, 1), "  ", Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.aporte.descricao_obrigatoria", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_sem_businessId_falha()
    {
        var resultado = AporteDeCapital.Criar("", Money.DeReais(1_000), new DateOnly(2026, 7, 1), "Aporte", Agora);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.aporte.business_obrigatorio", resultado.Erro.Codigo);
    }
}
