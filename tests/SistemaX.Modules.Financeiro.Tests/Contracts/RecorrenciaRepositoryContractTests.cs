using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;
using RecorrenciaAgg = SistemaX.Modules.Financeiro.Domain.Recorrencia.Recorrencia;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IRecorrenciaRepository"/>.</summary>
public abstract class RecorrenciaRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IRecorrenciaRepository CriarRepositorio();

    private static RecorrenciaAgg CriarRecorrencia(string businessId, string descricao, DateTimeOffset dataInicio)
        => RecorrenciaAgg.Criar(
            businessId, descricao, TipoContaRecorrente.APagar, Money.DeReais(1000), "aluguel",
            FrequenciaRecorrencia.Mensal, dataInicio, diaFixo: 5).Valor;

    [Fact]
    public async Task Salvar_e_buscar_retorna_a_mesma_recorrencia()
    {
        var repo = CriarRepositorio();
        var recorrencia = CriarRecorrencia(BusinessA, "Aluguel da loja", DateTimeOffset.UtcNow);

        await repo.SalvarAsync(recorrencia);
        var lido = await repo.BuscarAsync(BusinessA, recorrencia.Id);

        Assert.NotNull(lido);
        Assert.Equal(recorrencia.Id, lido!.Id);
        Assert.Equal(recorrencia.BusinessId, lido.BusinessId);
        Assert.Equal(recorrencia.Descricao, lido.Descricao);
        Assert.Equal(recorrencia.Tipo, lido.Tipo);
        Assert.Equal(recorrencia.ValorPrevisto, lido.ValorPrevisto);
        Assert.Equal(recorrencia.CategoriaId, lido.CategoriaId);
        Assert.Equal(recorrencia.DiaFixo, lido.DiaFixo);
        Assert.Equal(recorrencia.Frequencia, lido.Frequencia);
        Assert.Equal(recorrencia.DataInicio, lido.DataInicio);
        Assert.Equal(recorrencia.DataFim, lido.DataFim);
        Assert.Equal(recorrencia.Ativa, lido.Ativa);
        Assert.Equal(recorrencia.UltimaGeracaoEm, lido.UltimaGeracaoEm);
    }

    [Fact]
    public async Task Buscar_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.BuscarAsync(BusinessA, "recorrencia-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var recorrencia = CriarRecorrencia(BusinessA, "Salário", DateTimeOffset.UtcNow);
        await repo.SalvarAsync(recorrencia);

        Assert.Null(await repo.BuscarAsync(BusinessB, recorrencia.Id));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_registrar_geracao_e_desativar_reflete_novo_estado()
    {
        var repo = CriarRepositorio();
        var recorrencia = CriarRecorrencia(BusinessA, "Assinatura software", DateTimeOffset.UtcNow);
        await repo.SalvarAsync(recorrencia);

        var geradaEm = DateTimeOffset.UtcNow;
        recorrencia.RegistrarGeracao(geradaEm);
        recorrencia.Desativar();
        await repo.SalvarAsync(recorrencia);

        var lido = await repo.BuscarAsync(BusinessA, recorrencia.Id);
        Assert.Equal(geradaEm, lido!.UltimaGeracaoEm);
        Assert.False(lido.Ativa);
    }

    [Fact]
    public async Task ListarAtivasAsync_retorna_apenas_as_ativas_do_business()
    {
        var repo = CriarRepositorio();
        var ativa = CriarRecorrencia(BusinessA, "Ativa", DateTimeOffset.UtcNow);
        var inativa = CriarRecorrencia(BusinessA, "Inativa", DateTimeOffset.UtcNow);
        inativa.Desativar();
        var deOutroBusiness = CriarRecorrencia(BusinessB, "De outro tenant", DateTimeOffset.UtcNow);

        await repo.SalvarAsync(ativa);
        await repo.SalvarAsync(inativa);
        await repo.SalvarAsync(deOutroBusiness);

        var lista = await repo.ListarAtivasAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(ativa.Id, lista[0].Id);
    }
}
