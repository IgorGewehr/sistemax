using SistemaX.Modules.Financeiro.Application.Ativos;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;

namespace SistemaX.Modules.Financeiro.Tests.Ativos;

/// <summary>
/// "Um handler só, dois gates" (docs/financeiro/design-imobilizado-roi.md §2.2/§8.1) —
/// <see cref="CriarAtivoDeCapitalUseCase.ExecutarAsync"/> (alias da Análise por Projeto) e
/// <see cref="CriarAtivoDeCapitalUseCase.ExecutarImobilizadoAsync"/> (alias do Imobilizado) criam o
/// MESMO agregado por baixo, mas cada um só abre sob o SEU toggle — ligar um nunca libera o outro.
/// </summary>
public sealed class CriarAtivoDeCapitalUseCaseGatingTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static CriarAtivoDeCapitalComando NovoComando() => new(
        Biz, "Bancada ESD", NaturezaAtivo.Tangivel, CategoriaAtivo.Equipamento, 1_200_000, new DateOnly(2026, 7, 1), 60);

    private static (CriarAtivoDeCapitalUseCase UseCase, InMemoryConfiguracaoFinanceiraTenantRepository Configuracoes) NovoAmbiente()
    {
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        var useCase = new CriarAtivoDeCapitalUseCase(
            new InMemoryAtivoDeCapitalRepository(), new InMemoryContaAPagarRepository(),
            new InMemoryLancamentoContabilRepository(), configuracoes, new FakeRelogio(Agora));
        return (useCase, configuracoes);
    }

    [Fact]
    public async Task ExecutarAsync_ComAnalisePorProjetoDesligada_Falha422()
    {
        var (useCase, _) = NovoAmbiente();

        var resultado = await useCase.ExecutarAsync(NovoComando());

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projetos.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarImobilizadoAsync_ComImobilizadoRoiDesligado_Falha422()
    {
        var (useCase, _) = NovoAmbiente();

        var resultado = await useCase.ExecutarImobilizadoAsync(NovoComando());

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.imobilizado.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarImobilizadoAsync_ComSoAnalisePorProjetoLigada_AindaFalha()
    {
        // Ligar o toggle ERRADO não libera a rota do Imobilizado — os dois são independentes.
        var (useCase, configuracoes) = NovoAmbiente();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, analisePorProjetoAtiva: true).Valor);

        var resultado = await useCase.ExecutarImobilizadoAsync(NovoComando());

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.imobilizado.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarAsync_ComSoImobilizadoRoiLigado_AindaFalha()
    {
        var (useCase, configuracoes) = NovoAmbiente();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, imobilizadoRoiAtivo: true).Valor);

        var resultado = await useCase.ExecutarAsync(NovoComando());

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projetos.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarImobilizadoAsync_ComImobilizadoRoiLigado_CriaOMesmoAgregado()
    {
        var (useCase, configuracoes) = NovoAmbiente();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, imobilizadoRoiAtivo: true).Valor);

        var resultado = await useCase.ExecutarImobilizadoAsync(NovoComando());

        Assert.True(resultado.Sucesso);
        Assert.Equal(NaturezaAtivo.Tangivel, resultado.Valor.Natureza);
        Assert.Equal(CategoriaAtivo.Equipamento, resultado.Valor.Categoria);
    }

    [Fact]
    public async Task ExecutarAsync_ComAnalisePorProjetoLigada_CriaOMesmoAgregado()
    {
        var (useCase, configuracoes) = NovoAmbiente();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, analisePorProjetoAtiva: true).Valor);

        var resultado = await useCase.ExecutarAsync(NovoComando());

        Assert.True(resultado.Sucesso);
        Assert.Equal(NaturezaAtivo.Tangivel, resultado.Valor.Natureza);
    }
}
