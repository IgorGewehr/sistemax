using SistemaX.Modules.Financeiro.Application.Quant;

namespace SistemaX.Modules.Financeiro.Tests.Quant;

public sealed class RateioProporcionalTests
{
    [Fact]
    public void Aloca_proporcional_aos_pesos_quando_divisao_exata()
    {
        var resultado = RateioProporcional.Alocar(1_000, [500, 500]);
        Assert.Equal([500L, 500L], resultado);
    }

    [Fact]
    public void Soma_dos_alocados_e_sempre_igual_ao_total_mesmo_com_resto()
    {
        // 100 rateado entre pesos 1,1,1 -> 33.33 cada; soma tem que fechar em 100 exato.
        var resultado = RateioProporcional.Alocar(100, [1, 1, 1]);
        Assert.Equal(100, resultado.Sum());
        Assert.All(resultado, v => Assert.True(v is 33 or 34));
    }

    [Fact]
    public void Maior_peso_recebe_o_resto_de_arredondamento_primeiro()
    {
        // 10 rateado entre pesos 7 e 3: exato seria 7.0 e 3.0 -> sem resto, mudar pesos pra gerar resto.
        var resultado = RateioProporcional.Alocar(10, [2, 1]);
        // exato: 6.666 e 3.333 -> floor 6 e 3 = 9, falta 1 -> vai pro maior resto (peso 2, resto 0.666)
        Assert.Equal([7L, 3L], resultado);
    }

    [Fact]
    public void Total_zero_aloca_zero_para_todos()
    {
        var resultado = RateioProporcional.Alocar(0, [10, 20, 30]);
        Assert.Equal([0L, 0L, 0L], resultado);
    }

    [Fact]
    public void Pesos_todos_zero_distribui_igualmente_com_resto_no_inicio()
    {
        var resultado = RateioProporcional.Alocar(10, [0, 0, 0]);
        Assert.Equal(10, resultado.Sum());
        Assert.Equal([4L, 3L, 3L], resultado);
    }

    [Fact]
    public void Peso_unico_recebe_o_total_inteiro()
    {
        var resultado = RateioProporcional.Alocar(777, [42]);
        Assert.Equal([777L], resultado);
    }

    [Fact]
    public void Lista_de_pesos_vazia_retorna_lista_vazia()
    {
        var resultado = RateioProporcional.Alocar(500, []);
        Assert.Empty(resultado);
    }

    [Fact]
    public void Pesos_negativos_sao_tratados_como_zero_na_ponderacao()
    {
        var resultado = RateioProporcional.Alocar(100, [-50, 100]);
        Assert.Equal(100, resultado.Sum());
        Assert.Equal(100, resultado[1]); // todo o peso vai para o único peso positivo
        Assert.Equal(0, resultado[0]);
    }
}
