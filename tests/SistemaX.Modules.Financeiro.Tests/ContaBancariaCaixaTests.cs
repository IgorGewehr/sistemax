using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Invariantes do agregado <see cref="ContaBancariaCaixa"/> — o LAR ÚNICO de conta/caixa
/// da tela Bancário (docs/wiring/financeiro-telas-restantes.md §3).</summary>
public sealed class ContaBancariaCaixaTests
{
    [Fact]
    public void Criar_com_dados_validos_comeca_ativa_com_saldo_inicial_informado()
    {
        var resultado = ContaBancariaCaixa.Criar("biz-1", "Itaú PJ", TipoContaBancariaCaixa.ContaCorrente, new Money(812_000));

        Assert.True(resultado.Sucesso);
        var conta = resultado.Valor;
        Assert.True(conta.Ativa);
        Assert.Equal(new Money(812_000), conta.SaldoInicial);
        Assert.Equal(TipoContaBancariaCaixa.ContaCorrente, conta.Tipo);
        Assert.NotEmpty(conta.Id);
    }

    [Fact]
    public void Criar_sem_saldo_inicial_assume_zero()
    {
        var conta = ContaBancariaCaixa.Criar("biz-1", "Caixa", TipoContaBancariaCaixa.CaixaFisico).Valor;
        Assert.Equal(Money.Zero, conta.SaldoInicial);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Criar_sem_businessId_falha(string? businessId)
    {
        var resultado = ContaBancariaCaixa.Criar(businessId!, "Conta", TipoContaBancariaCaixa.ContaCorrente);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.conta_bancaria_caixa.business_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_sem_nome_falha()
    {
        var resultado = ContaBancariaCaixa.Criar("biz-1", "  ", TipoContaBancariaCaixa.ContaCorrente);
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.conta_bancaria_caixa.nome_invalido", resultado.Erro.Codigo);
    }

    [Fact]
    public void Criar_com_id_explicito_usa_o_id_informado()
    {
        var conta = ContaBancariaCaixa.Criar("biz-1", "Caixa", TipoContaBancariaCaixa.CaixaFisico, id: "conta-caixa-padrao").Valor;
        Assert.Equal("conta-caixa-padrao", conta.Id);
    }

    [Fact]
    public void Desativar_depois_reativar_alterna_o_estado()
    {
        var conta = ContaBancariaCaixa.Criar("biz-1", "Conta", TipoContaBancariaCaixa.ContaCorrente).Valor;

        var desativar = conta.Desativar();
        Assert.True(desativar.Sucesso);
        Assert.False(conta.Ativa);

        var reativar = conta.Reativar();
        Assert.True(reativar.Sucesso);
        Assert.True(conta.Ativa);
    }

    [Fact]
    public void Desativar_conta_ja_inativa_falha()
    {
        var conta = ContaBancariaCaixa.Criar("biz-1", "Conta", TipoContaBancariaCaixa.ContaCorrente).Valor;
        conta.Desativar();

        var resultado = conta.Desativar();
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.conta_bancaria_caixa.ja_inativa", resultado.Erro.Codigo);
    }

    [Fact]
    public void Reativar_conta_ja_ativa_falha()
    {
        var conta = ContaBancariaCaixa.Criar("biz-1", "Conta", TipoContaBancariaCaixa.ContaCorrente).Valor;

        var resultado = conta.Reativar();
        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.conta_bancaria_caixa.ja_ativa", resultado.Erro.Codigo);
    }

    [Fact]
    public void Reconstituir_nao_valida_e_preserva_o_estado_persistido()
    {
        var criadoEm = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var atualizadoEm = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        var conta = ContaBancariaCaixa.Reconstituir(
            "id-1", "biz-1", "Conta", TipoContaBancariaCaixa.CarteiraDigital, new Money(500), ativa: false, criadoEm, atualizadoEm);

        Assert.Equal("id-1", conta.Id);
        Assert.False(conta.Ativa);
        Assert.Equal(criadoEm, conta.CriadoEm);
        Assert.Equal(atualizadoEm, conta.AtualizadoEm);
    }
}
