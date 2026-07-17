using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Caixa;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova a agregação semanal do "Entrou × saiu por semana"
/// (docs/wiring/financeiro-telas-restantes.md §3): baldes de 7 dias corridos a partir de
/// `inicio`, com a última semana marcada `Parcial` quando o período pedido a corta antes dela
/// completar.</summary>
public sealed class MovimentosSemanaisServiceTests
{
    private const string BusinessId = "biz-1";

    [Fact]
    public async Task ListarAsync_agrupa_14_dias_em_2_semanas_completas()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new MovimentosSemanaisService(movimentos);

        var inicio = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 7, 14, 23, 59, 59, TimeSpan.Zero);

        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", "forma-1", "parcela-1", "origem-1",
            TipoMovimentoFinanceiro.Entrada, new Money(1_000), new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v1")).Valor);
        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", "forma-1", "parcela-2", "origem-2",
            TipoMovimentoFinanceiro.Saida, new Money(400), new DateTimeOffset(2026, 7, 2, 11, 0, 0, TimeSpan.Zero), new SourceRef("compras", "c1")).Valor);
        await movimentos.SalvarAsync(MovimentoFinanceiro.Registrar(
            BusinessId, "conta-1", "forma-1", "parcela-3", "origem-3",
            TipoMovimentoFinanceiro.Entrada, new Money(2_000), new DateTimeOffset(2026, 7, 9, 10, 0, 0, TimeSpan.Zero), new SourceRef("vendas", "v2")).Valor);

        var semanas = await servico.ListarAsync(BusinessId, inicio, fim);

        Assert.Equal(2, semanas.Count);

        var semana1 = semanas[0];
        Assert.Equal(1, semana1.Numero);
        Assert.Equal(new DateOnly(2026, 7, 1), semana1.Inicio);
        Assert.Equal(new DateOnly(2026, 7, 7), semana1.Fim);
        Assert.False(semana1.Parcial);
        Assert.Equal(7, semana1.Dias.Count);
        var dia2 = semana1.Dias.Single(d => d.Dia == new DateOnly(2026, 7, 2));
        Assert.Equal(new Money(1_000), dia2.Entradas);
        Assert.Equal(new Money(400), dia2.Saidas);

        var semana2 = semanas[1];
        Assert.Equal(2, semana2.Numero);
        Assert.Equal(new DateOnly(2026, 7, 8), semana2.Inicio);
        Assert.Equal(new DateOnly(2026, 7, 14), semana2.Fim);
        Assert.False(semana2.Parcial);
    }

    [Fact]
    public async Task ListarAsync_marca_ultima_semana_parcial_quando_fim_corta_antes_dos_7_dias()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new MovimentosSemanaisService(movimentos);

        var inicio = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 7, 4, 23, 59, 59, TimeSpan.Zero);

        var semanas = await servico.ListarAsync(BusinessId, inicio, fim);

        var semana = Assert.Single(semanas);
        Assert.True(semana.Parcial);
        Assert.Equal(4, semana.Dias.Count);
        Assert.Equal(new DateOnly(2026, 7, 4), semana.Fim);
    }

    [Fact]
    public async Task ListarAsync_dia_sem_movimento_aparece_com_zero()
    {
        var movimentos = new InMemoryMovimentoFinanceiroRepository();
        var servico = new MovimentosSemanaisService(movimentos);

        var inicio = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 7, 1, 23, 59, 59, TimeSpan.Zero);

        var semanas = await servico.ListarAsync(BusinessId, inicio, fim);

        var dia = Assert.Single(Assert.Single(semanas).Dias);
        Assert.Equal(Money.Zero, dia.Entradas);
        Assert.Equal(Money.Zero, dia.Saidas);
    }
}
