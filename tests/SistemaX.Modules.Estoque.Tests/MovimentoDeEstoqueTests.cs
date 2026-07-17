using SistemaX.Modules.Estoque.Domain.Comum;
using SistemaX.Modules.Estoque.Domain.Razao;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Estoque.Tests;

/// <summary>
/// O razão é a entidade central do módulo: append-only, quantidade sempre positiva exceto em
/// <c>Ajuste</c> (único tipo com delta assinado), e o efeito sobre físico/reservado é sempre
/// DERIVADO do <c>Tipo</c> — nunca do sinal armazenado.
/// </summary>
public class MovimentoDeEstoqueTests
{
    private static SourceRef OrigemDeTeste => new("manual", "mov-teste-1");

    [Theory]
    [InlineData(TipoMovimento.Entrada)]
    [InlineData(TipoMovimento.Saida)]
    [InlineData(TipoMovimento.Perda)]
    [InlineData(TipoMovimento.Reserva)]
    [InlineData(TipoMovimento.LiberacaoReserva)]
    public void Registrar_ComQuantidadeNaoPositiva_FalhaParaTiposQueNaoSaoAjuste(TipoMovimento tipo)
    {
        var resultado = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", tipo, Quantidade.Zero, Money.Zero,
            OrigemDeTeste, "chave-1", tipo == TipoMovimento.Perda ? "quebra" : "motivo", "op-1", "Operador",
            DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.movimento.quantidade_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Registrar_AjusteComDeltaZero_Falha()
    {
        var resultado = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Ajuste, Quantidade.Zero, Money.Zero,
            OrigemDeTeste, "chave-1", "correção", "op-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.movimento.ajuste_sem_delta", resultado.Erro.Codigo);
    }

    [Fact]
    public void Registrar_AjusteComDeltaNegativo_Permitido()
    {
        var resultado = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Ajuste, new Quantidade(-500), Money.Zero,
            OrigemDeTeste, "chave-1", "contagem divergente", "op-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(-500, resultado.Valor.EfeitoFisico.Milesimos);
    }

    [Fact]
    public void Registrar_PerdaSemMotivo_Falha()
    {
        var resultado = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Perda, Quantidade.DeInteiro(1), Money.Zero,
            OrigemDeTeste, "chave-1", "", "op-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.movimento.perda_sem_motivo", resultado.Erro.Codigo);
    }

    [Fact]
    public void Registrar_ComCustoNegativo_Falha()
    {
        var resultado = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", TipoMovimento.Entrada, Quantidade.DeInteiro(1), new Money(-100),
            OrigemDeTeste, "chave-1", "compra", "op-1", "Operador", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("estoque.movimento.custo_negativo", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData(TipoMovimento.Entrada, 1000, 0)]
    [InlineData(TipoMovimento.Saida, -1000, 0)]
    [InlineData(TipoMovimento.Perda, -1000, 0)]
    [InlineData(TipoMovimento.Reserva, 0, 1000)]
    [InlineData(TipoMovimento.LiberacaoReserva, 0, -1000)]
    public void EfeitoFisicoEReservado_SaoDerivadosDoTipo(TipoMovimento tipo, long efeitoFisicoEsperado, long efeitoReservadoEsperado)
    {
        var movimento = MovimentoDeEstoque.Registrar(
            "tenant-1", "principal", "produto-1", tipo, Quantidade.DeInteiro(1), Money.Zero,
            OrigemDeTeste, "chave-1", tipo == TipoMovimento.Perda ? "quebra" : "motivo", "op-1", "Operador",
            DateTimeOffset.UtcNow).Valor;

        Assert.Equal(efeitoFisicoEsperado, movimento.EfeitoFisico.Milesimos);
        Assert.Equal(efeitoReservadoEsperado, movimento.EfeitoReservado.Milesimos);
    }
}
