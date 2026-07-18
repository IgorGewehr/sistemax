using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Tempo;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>Contract test do port <see cref="IApontamentoDeTempoRepository"/> — roda 2× (InMemory +
/// SQLite).</summary>
public abstract class ApontamentoDeTempoRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IApontamentoDeTempoRepository CriarRepositorio();

    private static readonly DateTimeOffset Data = new(2026, 7, 17, 14, 0, 0, TimeSpan.FromHours(-3));

    private static ApontamentoDeTempo NovoApontamento(string businessId, int minutos = 30, string? clienteId = "cliente-1", string? projetoId = null)
        => ApontamentoDeTempo.Criar(
            businessId, minutos, Data, "operador-1", "Igor", Data,
            projetoId: projetoId, clienteId: clienteId, clienteNome: clienteId is null ? null : "Empresa X",
            descricao: "Suporte impressora fiscal").Valor;

    [Fact]
    public async Task Salvar_e_obter_retorna_o_mesmo_apontamento()
    {
        var repo = CriarRepositorio();
        var apontamento = NovoApontamento(BusinessA);

        await repo.SalvarAsync(apontamento);
        var lido = await repo.ObterPorIdAsync(BusinessA, apontamento.Id);

        Assert.NotNull(lido);
        Assert.Equal(30, lido!.Minutos);
        Assert.Equal("cliente-1", lido.ClienteId);
        Assert.Equal("Empresa X", lido.ClienteNome);
        Assert.Equal("operador-1", lido.OperadorId);
        Assert.Equal("Igor", lido.OperadorNome);
        Assert.Equal("Suporte impressora fiscal", lido.Descricao);
        Assert.Null(lido.CustoHoraCentavosSnapshot);
        Assert.Null(lido.CustoCentavos);
    }

    [Fact]
    public async Task Obter_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var apontamento = NovoApontamento(BusinessA);
        await repo.SalvarAsync(apontamento);

        Assert.Null(await repo.ObterPorIdAsync(BusinessB, apontamento.Id));
    }

    [Fact]
    public async Task ListarAsync_FiltraPorJanelaProjetoECliente()
    {
        var repo = CriarRepositorio();
        var doProjeto = NovoApontamento(BusinessA, 30, "cliente-1", "projeto-1");
        var deOutroCliente = NovoApontamento(BusinessA, 45, "cliente-2", "projeto-1");
        var deOutroProjeto = NovoApontamento(BusinessA, 15, "cliente-1", "projeto-2");
        await repo.SalvarAsync(doProjeto);
        await repo.SalvarAsync(deOutroCliente);
        await repo.SalvarAsync(deOutroProjeto);

        var de = Data.AddDays(-1);
        var ate = Data.AddDays(1);

        var porProjeto = await repo.ListarAsync(BusinessA, de, ate, projetoId: "projeto-1");
        Assert.Equal(2, porProjeto.Count);

        var porCliente = await repo.ListarAsync(BusinessA, de, ate, clienteId: "cliente-1");
        Assert.Equal(2, porCliente.Count);

        var porProjetoECliente = await repo.ListarAsync(BusinessA, de, ate, projetoId: "projeto-1", clienteId: "cliente-1");
        Assert.Single(porProjetoECliente);
        Assert.Equal(doProjeto.Id, porProjetoECliente[0].Id);

        var forDaJanela = await repo.ListarAsync(BusinessA, Data.AddDays(10), Data.AddDays(20));
        Assert.Empty(forDaJanela);
    }

    [Fact]
    public async Task ExcluirAsync_RemoveFisicamente()
    {
        var repo = CriarRepositorio();
        var apontamento = NovoApontamento(BusinessA);
        await repo.SalvarAsync(apontamento);

        var excluido = await repo.ExcluirAsync(BusinessA, apontamento.Id);
        Assert.True(excluido);
        Assert.Null(await repo.ObterPorIdAsync(BusinessA, apontamento.Id));
    }

    [Fact]
    public async Task ExcluirAsync_DeOutroBusiness_NaoRemoveEDevolveFalse()
    {
        var repo = CriarRepositorio();
        var apontamento = NovoApontamento(BusinessA);
        await repo.SalvarAsync(apontamento);

        var excluido = await repo.ExcluirAsync(BusinessB, apontamento.Id);
        Assert.False(excluido);
        Assert.NotNull(await repo.ObterPorIdAsync(BusinessA, apontamento.Id));
    }

    [Fact]
    public async Task ExcluirAsync_Inexistente_DevolveFalse()
    {
        var repo = CriarRepositorio();
        Assert.False(await repo.ExcluirAsync(BusinessA, "apontamento-que-nao-existe"));
    }
}
