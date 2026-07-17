using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Invariantes do agregado <see cref="SessaoCaixa"/> — o ritual de caixa físico
/// (docs/wiring/financeiro-telas-restantes.md §4).</summary>
public sealed class SessaoCaixaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 9, 0, 0, TimeSpan.Zero);

    private static SessaoCaixa AbrirSessao(long aberturaCentavos = 20_000)
        => SessaoCaixa.Abrir("biz-1", "conta-caixa-padrao", "op-1", "Ana", new Money(aberturaCentavos), Agora).Valor;

    [Fact]
    public void Abrir_com_dados_validos_comeca_Aberta_sem_movimentos()
    {
        var resultado = SessaoCaixa.Abrir("biz-1", "conta-caixa-padrao", "op-1", "Ana", new Money(20_000), Agora);

        Assert.True(resultado.Sucesso);
        var sessao = resultado.Valor;
        Assert.Equal(StatusSessaoCaixa.Aberta, sessao.Status);
        Assert.Empty(sessao.Movimentos);
        Assert.Equal(new Money(20_000), sessao.SaldoAbertura);
        Assert.Equal(new Money(20_000), sessao.SaldoEsperado);
        Assert.Null(sessao.SaldoInformado);
        Assert.Null(sessao.Diferenca);
        Assert.Null(sessao.FechadaEm);
        Assert.NotEmpty(sessao.Id);
    }

    [Fact]
    public void Abrir_com_saldo_de_abertura_negativo_falha()
    {
        var resultado = SessaoCaixa.Abrir("biz-1", "conta-caixa-padrao", "op-1", "Ana", new Money(-100), Agora);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.abertura_negativa", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Abrir_sem_operador_falha(string? operadorId)
    {
        var resultado = SessaoCaixa.Abrir("biz-1", "conta-caixa-padrao", operadorId!, "Ana", new Money(20_000), Agora);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.operador_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void Ciclo_completo_abrir_suprimento_venda_sangria_fechar_calcula_esperado_e_diferenca_certos()
    {
        var sessao = AbrirSessao(20_000); // abertura R$200

        Assert.True(sessao.RegistrarSuprimento(new Money(5_000), "reforço de troco", Agora, "op-1", "Ana").Sucesso); // +R$50
        Assert.True(sessao.RegistrarVendaEmEspecie(new Money(30_000), Agora, "op-1", "Ana").Sucesso); // +R$300
        Assert.True(sessao.RegistrarSangria(new Money(10_000), "depósito Itaú PJ", Agora, "op-1", "Ana").Sucesso); // -R$100

        // esperado = 200 + 50 + 300 - 100 = 450
        Assert.Equal(new Money(45_000), sessao.SaldoEsperado);
        Assert.Equal(new Money(35_000), sessao.TotalEntradas); // suprimento + venda
        Assert.Equal(new Money(10_000), sessao.TotalSaidas);
        Assert.Equal(3, sessao.Movimentos.Count);

        // fecha contando R$430 na gaveta — falta R$20
        var fechar = sessao.Fechar(new Money(43_000), Agora.AddHours(8));
        Assert.True(fechar.Sucesso);
        Assert.Equal(StatusSessaoCaixa.Fechada, sessao.Status);
        Assert.Equal(new Money(43_000), sessao.SaldoInformado);
        Assert.Equal(new Money(-2_000), sessao.Diferenca);
        Assert.NotNull(sessao.FechadaEm);
    }

    [Fact]
    public void Fechar_com_saldo_informado_maior_que_esperado_produz_diferenca_positiva_sobra()
    {
        var sessao = AbrirSessao(20_000);
        sessao.Fechar(new Money(20_500), Agora.AddHours(8));

        Assert.Equal(new Money(500), sessao.Diferenca);
    }

    [Fact]
    public void Sangria_que_excede_saldo_esperado_e_rejeitada()
    {
        var sessao = AbrirSessao(10_000); // R$100 na gaveta

        var resultado = sessao.RegistrarSangria(new Money(10_001), "tentando sacar mais do que tem", Agora, "op-1", "Ana");

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.sangria_excede_saldo", resultado.Erro.Codigo);
        Assert.Empty(sessao.Movimentos); // nada foi registrado
    }

    [Fact]
    public void Sangria_no_valor_exato_do_saldo_esperado_e_aceita_zera_a_gaveta()
    {
        var sessao = AbrirSessao(10_000);

        var resultado = sessao.RegistrarSangria(new Money(10_000), "fechamento antecipado", Agora, "op-1", "Ana");

        Assert.True(resultado.Sucesso);
        Assert.Equal(Money.Zero, sessao.SaldoEsperado);
    }

    [Fact]
    public void Sangria_sem_motivo_falha()
    {
        var sessao = AbrirSessao(10_000);
        var resultado = sessao.RegistrarSangria(new Money(1_000), "  ", Agora, "op-1", "Ana");
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.movimento.motivo_obrigatorio", resultado.Erro.Codigo);
    }

    [Fact]
    public void Nao_registra_movimento_em_sessao_fechada()
    {
        var sessao = AbrirSessao(10_000);
        sessao.Fechar(new Money(10_000), Agora.AddHours(8));

        var suprimento = sessao.RegistrarSuprimento(new Money(1_000), "motivo", Agora, "op-1", "Ana");
        var sangria = sessao.RegistrarSangria(new Money(1_000), "motivo", Agora, "op-1", "Ana");
        var venda = sessao.RegistrarVendaEmEspecie(new Money(1_000), Agora, "op-1", "Ana");

        Assert.True(suprimento.Falha);
        Assert.Equal("financeiro.sessao_caixa.status_invalido", suprimento.Erro.Codigo);
        Assert.True(sangria.Falha);
        Assert.Equal("financeiro.sessao_caixa.status_invalido", sangria.Erro.Codigo);
        Assert.True(venda.Falha);
        Assert.Equal("financeiro.sessao_caixa.status_invalido", venda.Erro.Codigo);
    }

    [Fact]
    public void Nao_fecha_uma_sessao_ja_fechada()
    {
        var sessao = AbrirSessao(10_000);
        Assert.True(sessao.Fechar(new Money(10_000), Agora.AddHours(8)).Sucesso);

        var segundoFechamento = sessao.Fechar(new Money(10_000), Agora.AddHours(9));

        Assert.True(segundoFechamento.Falha);
        Assert.Equal("financeiro.sessao_caixa.transicao_invalida", segundoFechamento.Erro.Codigo);
    }

    [Fact]
    public void Fechar_com_contagem_negativa_falha()
    {
        var sessao = AbrirSessao(10_000);
        var resultado = sessao.Fechar(new Money(-100), Agora.AddHours(8));
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.sessao_caixa.contagem_negativa", resultado.Erro.Codigo);
    }

    [Fact]
    public void Reconstituir_preserva_movimentos_e_totais_derivados()
    {
        var original = AbrirSessao(10_000);
        original.RegistrarSuprimento(new Money(5_000), "troco", Agora, "op-1", "Ana");

        var reidratada = SessaoCaixa.Reconstituir(
            original.Id, original.BusinessId, original.ContaCaixaId, original.OperadorId, original.OperadorNome,
            original.AbertaEm, original.SaldoAbertura, original.Status, original.Movimentos, original.FechadaEm, original.SaldoInformado);

        Assert.Equal(original.SaldoEsperado, reidratada.SaldoEsperado);
        Assert.Single(reidratada.Movimentos);
    }
}
