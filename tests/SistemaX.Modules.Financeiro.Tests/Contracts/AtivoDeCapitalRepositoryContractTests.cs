using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Ativos;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IAtivoDeCapitalRepository"/> — roda 2× (InMemory +
/// SQLite), mesmo molde de <c>ProjetoRepositoryContractTests</c>.</summary>
public abstract class AtivoDeCapitalRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IAtivoDeCapitalRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    private static AtivoDeCapital NovoDigiSat(string businessId, string? projetoId = "projeto-digisat")
        => AtivoDeCapital.Criar(
            businessId, "Licenças DigiSat 5×36m", NaturezaAtivo.Intangivel, CategoriaAtivo.LicencaSoftware,
            Money.DeReais(6_895), Money.Zero, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), 36, Agora,
            quantidadeUnidades: 5, projetoId: projetoId).Valor;

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_ativo()
    {
        var repo = CriarRepositorio();
        var ativo = NovoDigiSat(BusinessA);

        await repo.SalvarAsync(ativo);
        var lido = await repo.ObterPorIdAsync(BusinessA, ativo.Id);

        Assert.NotNull(lido);
        Assert.Equal(ativo.Id, lido!.Id);
        Assert.Equal("Licenças DigiSat 5×36m", lido.Nome);
        Assert.Equal(NaturezaAtivo.Intangivel, lido.Natureza);
        Assert.Equal(CategoriaAtivo.LicencaSoftware, lido.Categoria);
        Assert.Equal(689_500, lido.CustoAquisicao.Centavos);
        Assert.Equal(0, lido.ValorResidual.Centavos);
        Assert.Equal(new DateOnly(2026, 7, 1), lido.DataAquisicao);
        Assert.Equal(new DateOnly(2026, 7, 1), lido.InicioDepreciacao);
        Assert.Equal(36, lido.VidaUtilMeses);
        Assert.Equal(5, lido.QuantidadeUnidades);
        Assert.Equal("projeto-digisat", lido.ProjetoId);
        Assert.Equal(StatusAtivoDeCapital.EmUso, lido.Status);
        Assert.Null(lido.UltimaCompetenciaReconhecida);
    }

    [Fact]
    public async Task Obter_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var ativo = NovoDigiSat(BusinessA);
        await repo.SalvarAsync(ativo);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, ativo.Id));
    }

    [Fact]
    public async Task ListarAsync_FiltraPorProjeto()
    {
        var repo = CriarRepositorio();
        var doProjeto = NovoDigiSat(BusinessA, "projeto-digisat");
        var semProjeto = NovoDigiSat(BusinessA, null);
        await repo.SalvarAsync(doProjeto);
        await repo.SalvarAsync(semProjeto);

        var filtrado = await repo.ListarAsync(BusinessA, "projeto-digisat");
        Assert.Single(filtrado);
        Assert.Equal(doProjeto.Id, filtrado[0].Id);

        var todos = await repo.ListarAsync(BusinessA);
        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public async Task ListarEmUsoAsync_OmiteEncerradosEBaixados()
    {
        var repo = CriarRepositorio();
        var emUso = NovoDigiSat(BusinessA);
        var baixado = NovoDigiSat(BusinessA);
        baixado.Baixar("Cancelado", new DateOnly(2026, 7, 1), 689_500, Agora);

        await repo.SalvarAsync(emUso);
        await repo.SalvarAsync(baixado);

        var lista = await repo.ListarEmUsoAsync(BusinessA);
        Assert.Single(lista);
        Assert.Equal(emUso.Id, lista[0].Id);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_reconhecer_competencia_persiste_cursor()
    {
        var repo = CriarRepositorio();
        var ativo = NovoDigiSat(BusinessA);
        await repo.SalvarAsync(ativo);

        ativo.ReconhecerCompetencia(new DateOnly(2026, 7, 1), 19_153, Agora);
        await repo.SalvarAsync(ativo);

        var lido = await repo.ObterPorIdAsync(BusinessA, ativo.Id);
        Assert.NotNull(lido!.UltimaCompetenciaReconhecida);
        Assert.Equal(2026, lido.UltimaCompetenciaReconhecida!.Value.Year);
        Assert.Equal(7, lido.UltimaCompetenciaReconhecida.Value.Month);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_baixar_persiste_valor_reconhecido_na_baixa()
    {
        var repo = CriarRepositorio();
        var ativo = NovoDigiSat(BusinessA);
        await repo.SalvarAsync(ativo);

        ativo.Baixar("Contrato encerrado", new DateOnly(2026, 8, 1), 670_347, Agora);
        await repo.SalvarAsync(ativo);

        var lido = await repo.ObterPorIdAsync(BusinessA, ativo.Id);
        Assert.Equal(StatusAtivoDeCapital.Baixado, lido!.Status);
        Assert.Equal(670_347, lido.ValorReconhecidoNaBaixaCentavos);
        Assert.Equal("Contrato encerrado", lido.MotivoBaixa);
        Assert.NotNull(lido.BaixadoEm);
    }
}
