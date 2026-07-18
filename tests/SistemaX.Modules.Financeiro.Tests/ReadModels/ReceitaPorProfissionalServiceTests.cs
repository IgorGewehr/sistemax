using SistemaX.Modules.Financeiro.Application.Categorias;
using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Comum;
using SistemaX.Modules.Financeiro.Domain.ContasAPagarReceber;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.ReadModels;

/// <summary>
/// LENTE VERTICAL SERVIÇOS/BELEZA — receita/margem por técnico, sobre <c>ContaAReceber.TecnicoId</c>
/// (já persistido por <c>OsFaturadaHandler</c> desde P1-7 — zero dado novo).
/// </summary>
public sealed class ReceitaPorProfissionalServiceTests
{
    private const string BusinessId = "biz-receita-profissional";
    private static readonly DateTimeOffset Dia = new(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CalcularAsync_DuasOsDoMesmoTecnico_AgregaReceitaServicoEPecas()
    {
        var repo = new InMemoryContaAReceberRepository();

        var os1 = ContaAReceber.Criar(
            BusinessId, new SourceRef("appointment", "os-1"), "OS 1", CategoriaFinanceiraPadrao.Servicos,
            Dia, Money.DeReais(200), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(200), Dia),
            tecnicoId: "tec-1", corrente: CorrenteDeReceita.Servico,
            valorServico: Money.DeReais(150), valorPecas: Money.DeReais(50)).Valor;
        await repo.SalvarAsync(os1);

        var os2 = ContaAReceber.Criar(
            BusinessId, new SourceRef("appointment", "os-2"), "OS 2", CategoriaFinanceiraPadrao.Servicos,
            Dia, Money.DeReais(100), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(100), Dia),
            tecnicoId: "tec-1", corrente: CorrenteDeReceita.Servico,
            valorServico: Money.DeReais(100), valorPecas: Money.Zero).Valor;
        await repo.SalvarAsync(os2);

        var servico = new ReceitaPorProfissionalService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, Dia.AddDays(-1), Dia.AddDays(1));

        var linha = Assert.Single(linhas);
        Assert.Equal("tec-1", linha.TecnicoId);
        Assert.Equal(Money.DeReais(300).Centavos, linha.ReceitaTotalCentavos);
        Assert.Equal(Money.DeReais(250).Centavos, linha.ReceitaServicoCentavos);
        Assert.Equal(Money.DeReais(50).Centavos, linha.ReceitaPecasCentavos);
        Assert.Equal(2, linha.QuantidadeOs);
        Assert.Equal(Money.DeReais(250).Centavos, linha.MargemAproximadaCentavos); // = mão de obra (sem CMV de peça por técnico — gap documentado)
    }

    [Fact]
    public async Task CalcularAsync_VendaSemTecnico_NaoEntraNoAgrupamento()
    {
        var repo = new InMemoryContaAReceberRepository();

        var vendaSemTecnico = ContaAReceber.Criar(
            BusinessId, new SourceRef("sale", "venda-1"), "Venda balcão", CategoriaFinanceiraPadrao.Servicos,
            Dia, Money.DeReais(500), ContaFinanceiraBase.ParcelaUnica(Money.DeReais(500), Dia),
            corrente: CorrenteDeReceita.Comercio).Valor;
        await repo.SalvarAsync(vendaSemTecnico);

        var servico = new ReceitaPorProfissionalService(repo);
        var linhas = await servico.CalcularAsync(BusinessId, Dia.AddDays(-1), Dia.AddDays(1));

        Assert.Empty(linhas);
    }

    [Fact]
    public async Task CalcularAsync_SemContaNoPeriodo_DevolveListaVazia_FailQuiet()
    {
        var servico = new ReceitaPorProfissionalService(new InMemoryContaAReceberRepository());

        var linhas = await servico.CalcularAsync(BusinessId, Dia.AddDays(-30), Dia.AddDays(-20));

        Assert.Empty(linhas);
    }
}
