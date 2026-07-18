using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Projetos;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IProjetoRepository"/> — roda 2× (InMemory + SQLite),
/// mesmo molde de <c>AssinaturaRepositoryContractTests</c>.</summary>
public abstract class ProjetoRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IProjetoRepository CriarRepositorio();

    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 12, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_projeto()
    {
        var repo = CriarRepositorio();
        var projeto = Projeto.Criar(BusinessA, "DigiSat", "Revenda de licenças", Agora).Valor;

        await repo.SalvarAsync(projeto);
        var lido = await repo.ObterPorIdAsync(BusinessA, projeto.Id);

        Assert.NotNull(lido);
        Assert.Equal(projeto.Id, lido!.Id);
        Assert.Equal("DigiSat", lido.Nome);
        Assert.Equal("Revenda de licenças", lido.Descricao);
        Assert.Equal(StatusProjeto.Ativo, lido.Status);
        Assert.Equal(Agora, lido.CriadoEm);
        Assert.Null(lido.ArquivadoEm);
    }

    [Fact]
    public async Task Obter_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, "projeto-que-nao-existe"));
    }

    [Fact]
    public async Task Obter_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var projeto = Projeto.Criar(BusinessA, "DigiSat", null, Agora).Valor;
        await repo.SalvarAsync(projeto);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, projeto.Id));
    }

    [Fact]
    public async Task BuscarPorNomeAsync_e_case_insensitive_e_filtra_por_business()
    {
        var repo = CriarRepositorio();
        var projeto = Projeto.Criar(BusinessA, "DigiSat", null, Agora).Valor;
        await repo.SalvarAsync(projeto);

        var lido = await repo.BuscarPorNomeAsync(BusinessA, "digisat");
        Assert.NotNull(lido);
        Assert.Equal(projeto.Id, lido!.Id);

        Assert.Null(await repo.BuscarPorNomeAsync(BusinessB, "DigiSat"));
        Assert.Null(await repo.BuscarPorNomeAsync(BusinessA, "Aevo"));
    }

    [Fact]
    public async Task ListarAsync_sem_incluir_arquivados_omite_arquivados()
    {
        var repo = CriarRepositorio();
        var ativo = Projeto.Criar(BusinessA, "DigiSat", null, Agora).Valor;
        var arquivado = Projeto.Criar(BusinessA, "Projeto Antigo", null, Agora).Valor;
        arquivado.Arquivar(Agora.AddDays(10));

        await repo.SalvarAsync(ativo);
        await repo.SalvarAsync(arquivado);

        var lista = await repo.ListarAsync(BusinessA, incluirArquivados: false);
        Assert.Single(lista);
        Assert.Equal(ativo.Id, lista[0].Id);

        var listaCompleta = await repo.ListarAsync(BusinessA, incluirArquivados: true);
        Assert.Equal(2, listaCompleta.Count);
    }

    [Fact]
    public async Task Salvar_de_novo_apos_arquivar_reflete_novo_estado()
    {
        var repo = CriarRepositorio();
        var projeto = Projeto.Criar(BusinessA, "DigiSat", null, Agora).Valor;
        await repo.SalvarAsync(projeto);

        var quandoArquivou = Agora.AddDays(5);
        projeto.Arquivar(quandoArquivou);
        await repo.SalvarAsync(projeto);

        var lido = await repo.ObterPorIdAsync(BusinessA, projeto.Id);
        Assert.Equal(StatusProjeto.Arquivado, lido!.Status);
        Assert.Equal(quandoArquivou, lido.ArquivadoEm);
    }
}
