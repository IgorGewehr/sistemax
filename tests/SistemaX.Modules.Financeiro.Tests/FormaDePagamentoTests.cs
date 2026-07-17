using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Invariantes do agregado <see cref="FormaDePagamento"/> — o LAR ÚNICO de MDR/lag que
/// <c>FatoRecebiveisProjection</c> consulta (docs/wiring/financeiro-telas-restantes.md §3).</summary>
public sealed class FormaDePagamentoTests
{
    [Fact]
    public void Criar_com_dados_validos_comeca_ativa()
    {
        var resultado = FormaDePagamento.Criar("biz-1", "credito", TipoFormaPagamento.Credito, 0.0349m, 30, "conta-1");

        Assert.True(resultado.Sucesso);
        var forma = resultado.Valor;
        Assert.True(forma.Ativo);
        Assert.Equal(0.0349m, forma.TaxaPercentual);
        Assert.Equal(30, forma.PrazoCompensacaoDias);
        Assert.Equal("conta-1", forma.ContaLiquidacaoId);
    }

    [Fact]
    public void Criar_sem_contaLiquidacao_fica_nulo()
    {
        var forma = FormaDePagamento.Criar("biz-1", "pix", TipoFormaPagamento.Pix).Valor;
        Assert.Null(forma.ContaLiquidacaoId);
    }

    [Fact]
    public void Criar_sem_businessId_falha()
    {
        var resultado = FormaDePagamento.Criar("", "pix", TipoFormaPagamento.Pix);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.forma_pagamento.business_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_sem_nome_falha()
    {
        var resultado = FormaDePagamento.Criar("biz-1", "  ", TipoFormaPagamento.Pix);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.forma_pagamento.nome_invalido", resultado.Erro.Codigo);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Criar_com_taxa_fora_do_intervalo_falha(decimal taxa)
    {
        var resultado = FormaDePagamento.Criar("biz-1", "credito", TipoFormaPagamento.Credito, taxa);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.forma_pagamento.taxa_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_com_prazo_negativo_falha()
    {
        var resultado = FormaDePagamento.Criar("biz-1", "credito", TipoFormaPagamento.Credito, 0.03m, -1);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.forma_pagamento.prazo_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void CalcularValorLiquido_desconta_a_taxa_do_bruto()
    {
        var forma = FormaDePagamento.Criar("biz-1", "credito", TipoFormaPagamento.Credito, 0.0349m, 30).Valor;
        var liquido = forma.CalcularValorLiquido(Money.DeReais(100));

        Assert.Equal(Money.DeReais(100 - 3.49m), liquido);
    }

    [Fact]
    public void CalcularDataCompensacao_desloca_pelo_prazo()
    {
        var forma = FormaDePagamento.Criar("biz-1", "credito", TipoFormaPagamento.Credito, 0.0349m, 30).Valor;
        var data = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(data.AddDays(30), forma.CalcularDataCompensacao(data));
    }

    [Fact]
    public void Inativar_depois_ativar_alterna_o_estado()
    {
        var forma = FormaDePagamento.Criar("biz-1", "pix", TipoFormaPagamento.Pix).Valor;

        Assert.True(forma.Inativar().Sucesso);
        Assert.False(forma.Ativo);

        Assert.True(forma.Reativar().Sucesso);
        Assert.True(forma.Ativo);
    }

    [Fact]
    public void Inativar_forma_ja_inativa_falha()
    {
        var forma = FormaDePagamento.Criar("biz-1", "pix", TipoFormaPagamento.Pix).Valor;
        forma.Inativar();

        var resultado = forma.Inativar();
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.forma_pagamento.ja_inativa", resultado.Erro.Codigo);
    }

    [Fact]
    public void Reconstituir_nao_valida_e_preserva_o_estado_persistido()
    {
        var forma = FormaDePagamento.Reconstituir(
            "id-1", "biz-1", "credito", TipoFormaPagamento.Credito, 0.0349m, 30, "conta-1", ativo: false);

        Assert.Equal("id-1", forma.Id);
        Assert.False(forma.Ativo);
        Assert.Equal("conta-1", forma.ContaLiquidacaoId);
    }
}
