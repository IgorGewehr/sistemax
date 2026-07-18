using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Contracts;

/// <summary>
/// Contract test do port <see cref="IAssinaturaRepository"/> — exercitado por AMBOS os adapters
/// (in-memory e Sqlite), mesmo molde de <c>RecorrenciaRepositoryContractTests</c>. Antes deste
/// teste, <c>SqliteAssinaturaRepository</c> não tinha NENHUMA cobertura (era código morto: nunca
/// era wired em <c>FinanceiroInfrastructureModule</c>) — o round-trip de <c>Reconstituir</c> e a
/// criação da tabela nunca tinham sido exercitados de ponta a ponta.
/// </summary>
public abstract class AssinaturaRepositoryContractTests
{
    protected const string BusinessA = "biz-a";
    protected const string BusinessB = "biz-b";

    protected abstract IAssinaturaRepository CriarRepositorio();

    private static Assinatura CriarAssinatura(
        string businessId, string clienteNome, FrequenciaRecorrencia ciclo = FrequenciaRecorrencia.Mensal, int diaCobranca = 10)
        => Assinatura.Criar(
            businessId, clienteId: $"cliente-{clienteNome}", clienteNome, servicoId: "servico-1", servicoNome: "Plano Mensal",
            valorPorCiclo: Money.DeReais(150), ciclo, diaCobranca, dataInicio: new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-3))).Valor;

    [Fact]
    public async Task Salvar_e_buscar_retorna_a_mesma_assinatura()
    {
        var repo = CriarRepositorio();
        var assinatura = CriarAssinatura(BusinessA, "Ana", FrequenciaRecorrencia.Trimestral, diaCobranca: 15);

        await repo.SalvarAsync(assinatura);
        var lido = await repo.BuscarAsync(BusinessA, assinatura.Id);

        Assert.NotNull(lido);
        Assert.Equal(assinatura.Id, lido!.Id);
        Assert.Equal(assinatura.BusinessId, lido.BusinessId);
        Assert.Equal(assinatura.ClienteId, lido.ClienteId);
        Assert.Equal(assinatura.ClienteNome, lido.ClienteNome);
        Assert.Equal(assinatura.ServicoId, lido.ServicoId);
        Assert.Equal(assinatura.ServicoNome, lido.ServicoNome);
        Assert.Equal(assinatura.ValorPorCiclo, lido.ValorPorCiclo);
        Assert.Equal(assinatura.Ciclo, lido.Ciclo);
        Assert.Equal(assinatura.DiaCobranca, lido.DiaCobranca);
        Assert.Equal(assinatura.Status, lido.Status);
        Assert.Equal(assinatura.DataInicio, lido.DataInicio);
        Assert.Equal(assinatura.CanceladaEm, lido.CanceladaEm);
        Assert.Equal(assinatura.MotivoCancelamento, lido.MotivoCancelamento);
        Assert.Equal(assinatura.UltimaCobrancaGeradaEm, lido.UltimaCobrancaGeradaEm);
    }

    [Fact]
    public async Task Buscar_inexistente_retorna_null()
    {
        var repo = CriarRepositorio();
        Assert.Null(await repo.BuscarAsync(BusinessA, "assinatura-que-nao-existe"));
    }

    [Fact]
    public async Task Buscar_de_outro_business_nao_retorna()
    {
        var repo = CriarRepositorio();
        var assinatura = CriarAssinatura(BusinessA, "Bruno");
        await repo.SalvarAsync(assinatura);

        Assert.Null(await repo.BuscarAsync(BusinessB, assinatura.Id));
    }

    [Fact]
    public async Task Salvar_de_novo_apos_gerar_cobranca_e_cancelar_reflete_novo_estado()
    {
        var repo = CriarRepositorio();
        var assinatura = CriarAssinatura(BusinessA, "Carla");
        await repo.SalvarAsync(assinatura);

        var competencia = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        var cobranca = assinatura.GerarCobranca(competencia, "servicos");
        Assert.True(cobranca.Sucesso);

        var canceladaEm = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(-3));
        assinatura.Cancelar("Cliente pediu cancelamento", canceladaEm);
        await repo.SalvarAsync(assinatura);

        var lido = await repo.BuscarAsync(BusinessA, assinatura.Id);
        Assert.NotNull(lido);
        Assert.Equal(StatusAssinatura.Cancelada, lido!.Status);
        Assert.Equal(canceladaEm, lido.CanceladaEm);
        Assert.Equal("Cliente pediu cancelamento", lido.MotivoCancelamento);
        Assert.Equal(competencia, lido.UltimaCobrancaGeradaEm);
    }

    [Fact]
    public async Task ListarAtivasAsync_retorna_apenas_as_ativas_do_business()
    {
        var repo = CriarRepositorio();
        var ativa = CriarAssinatura(BusinessA, "Diego");
        var pausada = CriarAssinatura(BusinessA, "Elisa");
        pausada.Pausar(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-3)));
        var deOutroBusiness = CriarAssinatura(BusinessB, "Fabio");

        await repo.SalvarAsync(ativa);
        await repo.SalvarAsync(pausada);
        await repo.SalvarAsync(deOutroBusiness);

        var lista = await repo.ListarAtivasAsync(BusinessA);

        Assert.Single(lista);
        Assert.Equal(ativa.Id, lista[0].Id);
    }

    [Fact]
    public async Task ListarAsync_retorna_todas_do_business_independente_do_status()
    {
        var repo = CriarRepositorio();
        var ativa = CriarAssinatura(BusinessA, "Gustavo");
        var pausada = CriarAssinatura(BusinessA, "Helena");
        pausada.Pausar(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.FromHours(-3)));

        await repo.SalvarAsync(ativa);
        await repo.SalvarAsync(pausada);

        var lista = await repo.ListarAsync(BusinessA);

        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, a => a.Id == ativa.Id);
        Assert.Contains(lista, a => a.Id == pausada.Id);
    }
}
