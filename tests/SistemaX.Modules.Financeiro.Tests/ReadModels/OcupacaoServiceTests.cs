using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Tempo;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// LENTE VERTICAL SERVIÇOS/BELEZA — ocupação = horas apontadas ÷ horas disponíveis, por
/// profissional, sobre <see cref="ApontamentoDeTempo"/> (zero dado novo — mesma coleção que a
/// Análise por Projeto já usa, só agrupada por operador em vez de projeto/cliente).
/// </summary>
public sealed class OcupacaoServiceTests
{
    private const string BusinessId = "biz-ocupacao";

    [Fact]
    public async Task CalcularAsync_ApontamentosDeUmProfissional_ProdutividadeEhHorasSobreDisponiveis()
    {
        var repo = new InMemoryApontamentoDeTempoRepository();
        var inicio = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 8, 10, 23, 59, 59, TimeSpan.Zero); // janela de 9 dias corridos

        // 5 apontamentos de 240min (4h) cada = 1200min = 20h faturadas.
        for (var i = 0; i < 5; i++)
        {
            var apontamento = ApontamentoDeTempo.Criar(
                BusinessId, minutos: 240, data: inicio.AddDays(i), operadorId: "prof-1", operadorNome: "Fulana",
                criadoEm: inicio.AddDays(i), ordemServicoId: $"os-{i}").Valor;
            await repo.SalvarAsync(apontamento);
        }

        var servico = new OcupacaoService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, inicio, fim, horasDisponiveisPorDia: 8m);

        var linha = Assert.Single(linhas);
        Assert.Equal("prof-1", linha.OperadorId);
        Assert.Equal("Fulana", linha.OperadorNome);
        Assert.Equal(1_200, linha.MinutosApontados);
        Assert.Equal(20m, linha.HorasApontadas);
        Assert.Equal(72m, linha.HorasDisponiveis); // 8h × 9 dias corridos
        Assert.Equal(Math.Round(100m * 20 / 72, 1), linha.ProdutividadePercent);
    }

    [Fact]
    public async Task CalcularAsync_DoisProfissionais_UmMinutoEmCadaVaiParaSeuOperador()
    {
        var repo = new InMemoryApontamentoDeTempoRepository();
        var inicio = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var fim = new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

        await repo.SalvarAsync(ApontamentoDeTempo.Criar(
            BusinessId, 480, inicio, "prof-1", "Fulana", inicio, ordemServicoId: "os-1").Valor);
        await repo.SalvarAsync(ApontamentoDeTempo.Criar(
            BusinessId, 240, inicio, "prof-2", "Beltrano", inicio, ordemServicoId: "os-2").Valor);

        var servico = new OcupacaoService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, inicio, fim);

        Assert.Equal(2, linhas.Count);
        Assert.Equal(8m, linhas.Single(l => l.OperadorId == "prof-1").HorasApontadas);
        Assert.Equal(4m, linhas.Single(l => l.OperadorId == "prof-2").HorasApontadas);
    }

    [Fact]
    public async Task CalcularAsync_SemApontamentoNoPeriodo_DevolveListaVazia_FailQuiet()
    {
        var servico = new OcupacaoService(new InMemoryApontamentoDeTempoRepository());
        var inicio = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        var linhas = await servico.CalcularAsync(BusinessId, inicio, inicio.AddDays(10));

        Assert.Empty(linhas);
    }
}
