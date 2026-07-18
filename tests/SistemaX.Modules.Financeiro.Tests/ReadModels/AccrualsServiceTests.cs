using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// Ideia 3 do matemonstro (docs/financeiro/ideias-matemonstro.md) — Accruals = Lucro (competência,
/// via <see cref="DreGerencialService"/>) − Fluxo de Caixa Operacional (<c>fato_caixa_diario</c>,
/// já bilateral desde P1-3/Fatia 6). Nenhuma das duas lentes é nova — o read-model é a subtração.
/// </summary>
public sealed class AccrualsServiceTests
{
    private const string BusinessId = "business-1";
    private static readonly DateTimeOffset Inicio = new(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTimeOffset Fim = new(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(-3));

    private sealed record Ambiente(
        AccrualsService Servico,
        InMemoryContaAReceberRepository ContasAReceber,
        InMemoryFatoCaixaDiarioRepository FatoCaixaDiario);

    private static Ambiente NovoAmbiente()
    {
        var contasAReceber = new InMemoryContaAReceberRepository();
        var contasAPagar = new InMemoryContaAPagarRepository();
        var fatoCustoDiario = new InMemoryFatoCustoDiarioRepository();
        var fatoRecebiveis = new InMemoryFatoRecebiveisRepository();
        var fatoCaixaDiario = new InMemoryFatoCaixaDiarioRepository();

        var dre = new DreGerencialService(contasAReceber, contasAPagar, fatoCustoDiario, fatoRecebiveis);
        var servico = new AccrualsService(dre, fatoCaixaDiario);

        return new Ambiente(servico, contasAReceber, fatoCaixaDiario);
    }

    /// <summary>Venda a prazo, nenhum caixa ainda: lucro contábil de R$1.000,00 correu à frente do
    /// caixa (R$0,00 no período) — accruals positivo, o sinal clássico de "lucro no papel".</summary>
    [Fact]
    public async Task CalcularAsync_ComReceitaAPrazoSemCaixaAinda_AccrualsPositivo()
    {
        var ambiente = NovoAmbiente();

        var dataVenda = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.FromHours(-3));
        var venda = ContaAReceber.Criar(
            BusinessId, new SourceRef("teste", "venda-1"), "Venda a prazo", CategoriaFinanceiraPadrao.Servicos,
            dataVenda, Money.DeReais(1_000), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_000), dataVenda.AddDays(30))).Valor;
        await ambiente.ContasAReceber.SalvarAsync(venda);
        // Nenhum movimento de fato_caixa_diario no período — nada entrou ainda.

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(100_000, resultado.LucroDeCompetenciaCentavos);
        Assert.Equal(0, resultado.FluxoDeCaixaOperacionalCentavos);
        Assert.Equal(100_000, resultado.AccrualsCentavos);
        Assert.True(resultado.AccrualsCentavos > 0);
    }

    /// <summary>
    /// Pré-pagamento (P1-5, cobrança anual de assinatura): o cliente paga R$1.200,00 de uma vez —
    /// o CAIXA entra integral em agosto, mas o DRE só RECONHECE 1/12 (R$100,00) na competência de
    /// agosto (<c>ReceitaReconhecidaResolver</c>). Accruals fica NEGATIVO — caixa operacional à
    /// frente do lucro contábil, o espelho do caso "lucro sem caixa".
    /// </summary>
    [Fact]
    public async Task CalcularAsync_ComCaixaAdiantadoDeAssinaturaAnual_AccrualsNegativo()
    {
        var ambiente = NovoAmbiente();

        var dataCobranca = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var cobranca = ContaAReceber.Criar(
            BusinessId, new SourceRef("assinatura", "as1:202608"), "Plano Anual", CategoriaFinanceiraPadrao.ReceitaRecorrente,
            dataCobranca, Money.DeReais(1_200), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(1_200), dataCobranca),
            corrente: CorrenteDeReceita.Recorrente, mesesDeReconhecimento: 12).Valor;
        await ambiente.ContasAReceber.SalvarAsync(cobranca);

        // Cliente pagou o valor CHEIO na hora — caixa entra integral em agosto.
        var diaCobranca = new DateOnly(2026, 8, 5);
        await ambiente.FatoCaixaDiario.AcumularEntradaAsync(BusinessId, diaCobranca, Money.DeReais(1_200).Centavos);

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(10_000, resultado.LucroDeCompetenciaCentavos); // R$100,00 reconhecidos em agosto (1.200/12)
        Assert.Equal(120_000, resultado.FluxoDeCaixaOperacionalCentavos); // R$1.200,00 caiu no caixa
        Assert.Equal(10_000 - 120_000, resultado.AccrualsCentavos);
        Assert.True(resultado.AccrualsCentavos < 0);
    }

    [Fact]
    public async Task CalcularAsync_SemMovimentoNenhum_AccrualsZero()
    {
        var ambiente = NovoAmbiente();

        var resultado = await ambiente.Servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(0, resultado.LucroDeCompetenciaCentavos);
        Assert.Equal(0, resultado.FluxoDeCaixaOperacionalCentavos);
        Assert.Equal(0, resultado.AccrualsCentavos);
    }
}
