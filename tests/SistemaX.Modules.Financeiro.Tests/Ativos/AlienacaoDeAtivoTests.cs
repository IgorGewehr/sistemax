using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Contabil;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// Fatia I4 — Alienação (venda) de <see cref="AtivoDeCapital"/> (docs/financeiro/design-imobilizado-roi.md
/// §3.2/§4.6/§14): FSM <c>EmUso|Encerrado → Vendido</c>, <c>ResultadoAlienacao</c> informativo, e a
/// divergência de leitura em <see cref="AtivoDeCapitalQuant.SomaNaJanela"/> entre write-off
/// (lump sum, DENTRO do D&amp;A) e venda (fatia linear normal, FORA — DI6).
/// </summary>
public sealed class AlienacaoDeAtivoTests
{
    private const string Biz = "assistencia-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static AtivoDeCapital CriarBancada()
        => AtivoDeCapital.Criar(
            Biz, "Bancada ESD", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(12_000), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 60, Agora).Valor;

    [Fact]
    public void Baixar_ComValorVenda_TransicionaParaVendidoEGuardaOPreco()
    {
        var ativo = CriarBancada();

        var resultado = ativo.Baixar("Upgrade de bancada", new DateOnly(2026, 7, 1), 1_200_000, Agora, Money.DeReais(9_000));

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Vendido, ativo.Status);
        Assert.Equal(900_000, ativo.ValorVenda!.Value.Centavos);
        Assert.Equal(1_200_000, ativo.ValorReconhecidoNaBaixaCentavos);
    }

    [Fact]
    public void Baixar_SemValorVenda_ContinuaTransicionandoParaBaixado()
    {
        // Regressão: comportamento pré-I4 intocado quando `valorVenda` é omitido.
        var ativo = CriarBancada();

        var resultado = ativo.Baixar("Sucateado", new DateOnly(2026, 7, 1), 1_200_000, Agora);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Baixado, ativo.Status);
        Assert.Null(ativo.ValorVenda);
    }

    [Fact]
    public void Baixar_ComVenda_ResultadoAlienacao_ComGanho_EhPositivo()
    {
        var ativo = CriarBancada();
        // Valor contábil informado (1.100.000) < preço de venda (1.300.000) → ganho de 200.000.
        ativo.Baixar("Venda com ganho", new DateOnly(2026, 8, 1), 1_100_000, Agora, new Money(1_300_000));

        Assert.Equal(200_000, ativo.ResultadoAlienacaoCentavos);
    }

    [Fact]
    public void Baixar_ComVenda_ResultadoAlienacao_ComPerda_EhNegativo()
    {
        var ativo = CriarBancada();
        ativo.Baixar("Venda com perda", new DateOnly(2026, 8, 1), 1_100_000, Agora, new Money(700_000));

        Assert.Equal(-400_000, ativo.ResultadoAlienacaoCentavos);
    }

    [Fact]
    public void ResultadoAlienacao_ForaDeVendido_EhNull()
    {
        var ativo = CriarBancada();
        Assert.Null(ativo.ResultadoAlienacaoCentavos); // EmUso

        ativo.Baixar("Sucateado", new DateOnly(2026, 7, 1), 1_200_000, Agora); // Baixado (write-off)
        Assert.Null(ativo.ResultadoAlienacaoCentavos);
    }

    [Fact]
    public void Fsm_EncerradoTambemPodeSerVendido()
    {
        var ativo = AtivoDeCapital.Criar(
            Biz, "Computador", NaturezaAtivo.Tangivel, CategoriaAtivo.Computador,
            Money.DeReais(6_000), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 1, Agora).Valor;
        ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 600_000, Agora);
        Assert.Equal(StatusAtivoDeCapital.Encerrado, ativo.Status);

        var resultado = ativo.Baixar("Vendido usado", new DateOnly(2026, 8, 1), 0, Agora, Money.DeReais(500));

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Vendido, ativo.Status);
    }

    [Fact]
    public void Fsm_BaixadoNaoPodeSerVendidoDeNovo_Terminal()
    {
        var ativo = CriarBancada();
        ativo.Baixar("Sucateado", new DateOnly(2026, 7, 1), 1_200_000, Agora); // → Baixado

        var resultado = ativo.Baixar("Vender depois", new DateOnly(2026, 8, 1), 0, Agora, Money.DeReais(100));

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.transicao_invalida", resultado.Erro.Codigo);
    }

    [Fact]
    public void Fsm_VendidoNaoPodeSerVendidoOuBaixadoDeNovo_Terminal()
    {
        var ativo = CriarBancada();
        ativo.Baixar("Venda 1", new DateOnly(2026, 7, 1), 1_200_000, Agora, Money.DeReais(9_000));

        var segundaVenda = ativo.Baixar("Venda 2", new DateOnly(2026, 8, 1), 0, Agora, Money.DeReais(1));
        Assert.True(segundaVenda.Falha);
        Assert.Equal("financeiro.ativo.transicao_invalida", segundaVenda.Erro.Codigo);

        var baixaDepoisDeVendido = ativo.Baixar("Sucatear depois de vendido", new DateOnly(2026, 8, 1), 0, Agora);
        Assert.True(baixaDepoisDeVendido.Falha);
        Assert.Equal("financeiro.ativo.transicao_invalida", baixaDepoisDeVendido.Erro.Codigo);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // AtivoDeCapitalQuant — a divergência de leitura entre write-off (lump sum, dentro do D&A) e
    // venda (fatia linear normal, fora — DI6).
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SomaNaJanela_Vendido_NoMesDaVenda_SoAFatiaLinearNormal_SemLumpSum()
    {
        var ativo = CriarBancada(); // R$12.000/60m = R$200,00/mês exato
        var valorContabilAntes = ativo.CustoAquisicao.Centavos - AtivoDeCapitalQuant.ReconhecidoAteOCursor(ativo);
        ativo.Baixar("Venda", new DateOnly(2026, 7, 1), valorContabilAntes, Agora, Money.DeReais(9_000));

        var somaMesVenda = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        // Só a parcela linear do mês (20.000 centavos) — NUNCA o valor contábil inteiro (1.200.000).
        Assert.Equal(20_000, somaMesVenda);
    }

    [Fact]
    public void SomaNaJanela_Vendido_NenhumaCompetenciaPosteriorReconheceNada()
    {
        var ativo = CriarBancada();
        ativo.Baixar("Venda", new DateOnly(2026, 7, 1), 1_200_000, Agora, Money.DeReais(9_000));

        var somaDepois = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 8, 1), new DateOnly(2031, 6, 30));

        Assert.Equal(0, somaDepois);
    }

    [Fact]
    public void ValorContabilAtualCentavos_ZeraAposVenda_MesmoComResultadoAlienacaoNaoZero()
    {
        var ativo = CriarBancada();
        // Vende no primeiro mês, ANTES de qualquer competência ter sido reconhecida — valor contábil
        // "real" seria 1.200.000, mas o painel/balanço mostra ZERO: o bem saiu do livro (invariante #8).
        ativo.Baixar("Venda", new DateOnly(2026, 7, 1), 1_200_000, Agora, Money.DeReais(9_000));

        Assert.Equal(0, AtivoDeCapitalQuant.ValorContabilAtualCentavos(ativo));
        Assert.NotNull(ativo.ResultadoAlienacaoCentavos); // o número não desapareceu — só saiu do balanço
    }

    [Fact]
    public void SomaNaJanela_Baixado_ContinuaReconhecendoOLumpSumInteiro_RegressaoI4()
    {
        // Regressão explícita: write-off (Baixado) PERMANECE com o comportamento pré-I4 — o valor
        // contábil inteiro entra no D&A daquele mês (DI6: impairment fica DENTRO do resultado).
        var ativo = CriarBancada();
        ativo.Baixar("Sucateado", new DateOnly(2026, 7, 1), 1_200_000, Agora);

        var somaMesBaixa = AtivoDeCapitalQuant.SomaNaJanela(ativo, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31));

        Assert.Equal(1_200_000, somaMesBaixa);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────
    // LancamentoContabilFactory.DeVendaDeAtivoDeCapital — §4.6: "C-1.3 pelo valor contábil,
    // D-4.1 pela perda (ou crédito residual em 3.1 pelo ganho)".
    // ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeVendaDeAtivoDeCapital_ComGanho_CreditaReceitaEBalanceaAsPartidas()
    {
        var resultado = LancamentoContabilFactory.DeVendaDeAtivoDeCapital(
            Biz, Agora, "Venda — Bancada", "ativo-1", valorVenda: Money.DeReais(9_000), valorContabil: Money.DeReais(7_000));

        Assert.True(resultado.Sucesso);
        var lancamento = resultado.Valor;
        Assert.Equal(lancamento.TotalDebito, lancamento.TotalCredito);
        Assert.Equal(Money.DeReais(9_000), lancamento.TotalDebito);

        Assert.Contains(lancamento.Partidas, p => p.ContaContabilId == PlanoDeContasPadrao.AtivosDeCapital.Id && p.Natureza == NaturezaPartida.Credito && p.Valor == Money.DeReais(7_000));
        Assert.Contains(lancamento.Partidas, p => p.ContaContabilId == PlanoDeContasPadrao.Receita.Id && p.Natureza == NaturezaPartida.Credito && p.Valor == Money.DeReais(2_000));
        Assert.Contains(lancamento.Partidas, p => p.ContaContabilId == PlanoDeContasPadrao.ContasAReceber.Id && p.Natureza == NaturezaPartida.Debito && p.Valor == Money.DeReais(9_000));
    }

    [Fact]
    public void DeVendaDeAtivoDeCapital_ComPerda_DebitaCustoDespesaEBalanceaAsPartidas()
    {
        var resultado = LancamentoContabilFactory.DeVendaDeAtivoDeCapital(
            Biz, Agora, "Venda — Bancada", "ativo-1", valorVenda: Money.DeReais(3_000), valorContabil: Money.DeReais(7_000));

        Assert.True(resultado.Sucesso);
        var lancamento = resultado.Valor;
        Assert.Equal(lancamento.TotalDebito, lancamento.TotalCredito);
        Assert.Equal(Money.DeReais(7_000), lancamento.TotalDebito);

        Assert.Contains(lancamento.Partidas, p => p.ContaContabilId == PlanoDeContasPadrao.CustoDespesa.Id && p.Natureza == NaturezaPartida.Debito && p.Valor == Money.DeReais(4_000));
    }

    [Fact]
    public void DeVendaDeAtivoDeCapital_ValorVendaIgualAoContabil_SemPartidaDeResultado()
    {
        var resultado = LancamentoContabilFactory.DeVendaDeAtivoDeCapital(
            Biz, Agora, "Venda — Bancada", "ativo-1", valorVenda: Money.DeReais(7_000), valorContabil: Money.DeReais(7_000));

        Assert.True(resultado.Sucesso);
        var lancamento = resultado.Valor;
        Assert.Equal(2, lancamento.Partidas.Count); // só Dr 1.2 / Cr 1.3, nenhuma perna de ganho/perda
        Assert.DoesNotContain(lancamento.Partidas, p => p.ContaContabilId == PlanoDeContasPadrao.Receita.Id || p.ContaContabilId == PlanoDeContasPadrao.CustoDespesa.Id);
    }

    [Fact]
    public void DeVendaDeAtivoDeCapital_AmbosZero_FalhaPorPartidasInsuficientes()
    {
        var resultado = LancamentoContabilFactory.DeVendaDeAtivoDeCapital(
            Biz, Agora, "Venda — Bancada", "ativo-1", valorVenda: Money.Zero, valorContabil: Money.Zero);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.lancamento.partidas_insuficientes", resultado.Erro.Codigo);
    }
}
