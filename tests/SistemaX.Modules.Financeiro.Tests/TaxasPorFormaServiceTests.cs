using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova o painel "Ver por forma" do Super Consultor Bancário
/// (docs/wiring/financeiro-telas-restantes.md §3): volume × MDR por forma, só sobre entradas —
/// nenhum número hardcoded, tudo vem de <c>FormaDePagamento.TaxaPercentual</c>.</summary>
public sealed class TaxasPorFormaServiceTests
{
    private const string BusinessId = "biz-1";
    private static readonly DateTimeOffset Inicio = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = new(2026, 7, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task CalcularAsync_agrupa_por_forma_e_aplica_o_mdr_de_cada_uma()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formas = new InMemoryFormaDePagamentoRepository();
        var servico = new TaxasPorFormaService(movimentos, formas);

        var pix = FormaDePagamento.Criar(BusinessId, "pix", TipoFormaPagamento.Pix, taxaPercentual: 0m).Valor;
        var credito = FormaDePagamento.Criar(BusinessId, "credito", TipoFormaPagamento.Credito, taxaPercentual: 0.0349m).Valor;
        await formas.SalvarAsync(pix);
        await formas.SalvarAsync(credito);

        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", pix.Id, "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(10_000), new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v1")).Valor);
        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", credito.Id, "parcela-2", "origem-2",
            TipoMovimentoFinanceiro.Entrada, new Money(20_000), new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v2")).Valor);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(2, resumo.PorForma.Count);
        var linhaPix = resumo.PorForma.Single(p => p.Forma == "pix");
        Assert.Equal(new Money(10_000), linhaPix.Volume);
        Assert.Equal(Money.Zero, linhaPix.Taxa);

        var linhaCredito = resumo.PorForma.Single(p => p.Forma == "credito");
        Assert.Equal(new Money(20_000), linhaCredito.Volume);
        Assert.Equal(credito.CalcularTaxa(new Money(20_000)), linhaCredito.Taxa);

        Assert.Equal(linhaPix.Taxa + linhaCredito.Taxa, resumo.TaxaTotal);
        Assert.Equal(new Money(30_000), resumo.VolumeTotal);
    }

    [Fact]
    public async Task CalcularAsync_ignora_saidas_pois_nao_ha_mdr_sobre_dinheiro_saindo()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formas = new InMemoryFormaDePagamentoRepository();
        var servico = new TaxasPorFormaService(movimentos, formas);

        var credito = FormaDePagamento.Criar(BusinessId, "credito", TipoFormaPagamento.Credito, taxaPercentual: 0.0349m).Valor;
        await formas.SalvarAsync(credito);

        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", credito.Id, "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Saida, new Money(5_000), new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), new SourceRef("compras", "c1")).Valor);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Empty(resumo.PorForma);
        Assert.Equal(Money.Zero, resumo.TaxaTotal);
    }

    [Fact]
    public async Task CalcularAsync_sem_movimentos_devolve_zero_sem_dividir_por_zero()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var formas = new InMemoryFormaDePagamentoRepository();
        var servico = new TaxasPorFormaService(movimentos, formas);

        var resumo = await servico.CalcularAsync(BusinessId, Inicio, Fim);

        Assert.Equal(0m, resumo.PercentualVolume);
        Assert.Empty(resumo.PorForma);
    }
}
