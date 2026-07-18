using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// <see cref="BaixarAtivoDeCapitalUseCase"/> — fatia I4 (docs/financeiro/design-imobilizado-roi.md
/// §4.6/§8.1): "um handler só" continua valendo — <c>ValorVendaCentavos</c> não-nulo é o único
/// diferencial entre write-off comum (comportamento pré-I4, intocado) e alienação (gera TAMBÉM a
/// <c>ContaAReceber</c> categoria <c>alienacao-de-ativo</c> e o lançamento contábil único da venda).
/// </summary>
public sealed class BaixarAtivoDeCapitalUseCaseTests
{
    private const string Biz = "assistencia-1";
    private static readonly DateTimeOffset Agora = new(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    private sealed record Ambiente(
        InMemoryAtivoDeCapitalRepository Ativos, InMemoryContaAReceberRepository ContasAReceber,
        InMemoryLancamentoContabilRepository Lancamentos, BaixarAtivoDeCapitalUseCase UseCase);

    private static async Task<Ambiente> NovoAmbienteComBancadaAsync()
    {
        var ativos = new InMemoryAtivoDeCapitalRepository();
        var contasAReceber = new InMemoryContaAReceberRepository();
        var lancamentos = new InMemoryLancamentoContabilRepository();
        var useCase = new BaixarAtivoDeCapitalUseCase(ativos, contasAReceber, lancamentos, new FakeRelogio(Agora));

        var ativo = AtivoDeCapital.Criar(
            Biz, "Bancada ESD", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento,
            Money.DeReais(12_000), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 60, Agora).Valor;
        await ativos.SalvarAsync(ativo);

        return new Ambiente(ativos, contasAReceber, lancamentos, useCase);
    }

    [Fact]
    public async Task ExecutarAsync_SemValorVenda_ComportamentoPreI4Intocado_NenhumaContaOuLancamentoDeVenda()
    {
        var ambiente = await NovoAmbienteComBancadaAsync();
        var ativo = (await ambiente.Ativos.ListarAsync(Biz)).Single();

        var comando = new BaixarAtivoDeCapitalComando(Biz, ativo.Id, "Sucateado", new DateOnly(2026, 8, 1));
        var resultado = await ambiente.UseCase.ExecutarAsync(comando);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Baixado, resultado.Valor.Status);

        var contasCriadas = await ambiente.ContasAReceber.ListarPorCompetenciaAsync(Biz, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Assert.Empty(contasCriadas);
    }

    [Fact]
    public async Task ExecutarAsync_ComValorVenda_TransicionaParaVendidoECriaContaAReceberDaAlienacao()
    {
        var ambiente = await NovoAmbienteComBancadaAsync();
        var ativo = (await ambiente.Ativos.ListarAsync(Biz)).Single();

        var comando = new BaixarAtivoDeCapitalComando(Biz, ativo.Id, "Upgrade de bancada", new DateOnly(2026, 8, 1), ValorVendaCentavos: 900_000);
        var resultado = await ambiente.UseCase.ExecutarAsync(comando);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusAtivoDeCapital.Vendido, resultado.Valor.Status);
        Assert.Equal(900_000, resultado.Valor.ValorVenda!.Value.Centavos);

        var contasCriadas = await ambiente.ContasAReceber.ListarPorCompetenciaAsync(Biz, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        var conta = Assert.Single(contasCriadas);
        Assert.Equal(CategoriaFinanceiraPadrao.AlienacaoDeAtivo, conta.CategoriaId);
        Assert.Equal(900_000, conta.ValorTotal.Centavos);
    }

    [Fact]
    public async Task ExecutarAsync_ComValorVenda_GeraLancamentoContabilBalanceadoDaAlienacao()
    {
        var ambiente = await NovoAmbienteComBancadaAsync();
        var ativo = (await ambiente.Ativos.ListarAsync(Biz)).Single();

        var comando = new BaixarAtivoDeCapitalComando(Biz, ativo.Id, "Venda", new DateOnly(2026, 8, 1), ValorVendaCentavos: 900_000);
        await ambiente.UseCase.ExecutarAsync(comando);

        var lancamento = await ambiente.Lancamentos.BuscarPorOrigemAsync(Biz, $"financeiro.alienacao-ativo:{ativo.Id}");
        Assert.NotNull(lancamento);
        Assert.Equal(lancamento!.TotalDebito, lancamento.TotalCredito);
    }

    [Fact]
    public async Task ExecutarAsync_AtivoInexistente_Falha()
    {
        var ambiente = await NovoAmbienteComBancadaAsync();

        var comando = new BaixarAtivoDeCapitalComando(Biz, "id-inexistente", "Motivo", new DateOnly(2026, 8, 1));
        var resultado = await ambiente.UseCase.ExecutarAsync(comando);

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.ativo.nao_encontrado", resultado.Erro.Codigo);
    }
}
