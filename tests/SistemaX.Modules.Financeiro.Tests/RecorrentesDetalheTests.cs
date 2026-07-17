using SistemaX.Modules.Financeiro.Application.ReadModels;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests;

/// <summary>Prova o detalhe por assinatura e a lente "Contas fixas" da tela Recorrentes
/// (docs/wiring/financeiro-telas-restantes.md §2/§C): só lista o que está ATIVO e nunca vaza
/// entre tenants (R1).</summary>
public sealed class RecorrentesDetalheTests
{
    private const string BusinessId = "biz-1";
    private const string OutroBusinessId = "biz-2";
    private static readonly DateTimeOffset Referencia = new(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AssinaturaDetalheService_ListaSoAtivas_ComProximaCobrancaNoFuturo()
    {
        var repo = new InMemoryAssinaturaRepository();

        var ativa = Assinatura.Criar(
            BusinessId, "cli-1", "Mercado Ideal", "srv-1", "ServicePro", new Money(1_047_00),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: Referencia.AddMonths(-3)).Valor;
        await repo.SalvarAsync(ativa);

        var cancelada = Assinatura.Criar(
            BusinessId, "cli-2", "Padaria Pão Quente", "srv-2", "Brain", new Money(440_00),
            FrequenciaRecorrencia.Mensal, diaCobranca: 10, dataInicio: Referencia.AddMonths(-6)).Valor;
        cancelada.Cancelar("cliente cancelou", Referencia);
        await repo.SalvarAsync(cancelada);

        var servico = new AssinaturaDetalheService(repo);
        var resultado = await servico.ListarAtivasAsync(BusinessId, Referencia);

        var detalhe = Assert.Single(resultado);
        Assert.Equal(ativa.Id, detalhe.Id);
        Assert.Equal("Mercado Ideal", detalhe.ClienteNome);
        Assert.Equal("ServicePro", detalhe.ServicoNome);
        Assert.Equal(new Money(1_047_00), detalhe.ValorPorCiclo);
        Assert.True(detalhe.ProximaCobranca > Referencia);
        Assert.Equal(5, detalhe.ProximaCobranca.Day);
    }

    [Fact]
    public async Task AssinaturaDetalheService_NuncaVazaAssinaturaDeOutroBusinessId()
    {
        var repo = new InMemoryAssinaturaRepository();
        var doOutroTenant = Assinatura.Criar(
            OutroBusinessId, "cli-x", "Outro tenant", "srv-x", "Serviço X", new Money(500_00),
            FrequenciaRecorrencia.Mensal, diaCobranca: 1, dataInicio: Referencia.AddMonths(-1)).Valor;
        await repo.SalvarAsync(doOutroTenant);

        var servico = new AssinaturaDetalheService(repo);
        var resultado = await servico.ListarAtivasAsync(BusinessId, Referencia);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ContasFixasService_ListaSoRecorrenciasAtivas_ComProximaOcorrencia()
    {
        var repo = new InMemoryRecorrenciaRepository();

        var aluguel = Recorrencia.Criar(
            BusinessId, "Aluguel da loja", TipoContaRecorrente.APagar, new Money(2_100_00),
            "aluguel", FrequenciaRecorrencia.Mensal, Referencia.AddMonths(-2), diaFixo: 28).Valor;
        await repo.SalvarAsync(aluguel);

        var inativa = Recorrencia.Criar(
            BusinessId, "Assinatura cancelada", TipoContaRecorrente.APagar, new Money(300_00),
            "software", FrequenciaRecorrencia.Mensal, Referencia.AddMonths(-2), diaFixo: 1).Valor;
        inativa.Desativar();
        await repo.SalvarAsync(inativa);

        var servico = new ContasFixasService(repo);
        var resultado = await servico.ListarAsync(BusinessId);

        var contaFixa = Assert.Single(resultado);
        Assert.Equal("Aluguel da loja", contaFixa.Descricao);
        Assert.Equal(new Money(2_100_00), contaFixa.ValorPrevisto);
        Assert.NotNull(contaFixa.ProximaOcorrencia);
    }

    [Fact]
    public async Task ContasFixasService_NuncaVazaRecorrenciaDeOutroBusinessId()
    {
        var repo = new InMemoryRecorrenciaRepository();
        var doOutroTenant = Recorrencia.Criar(
            OutroBusinessId, "Não deve aparecer", TipoContaRecorrente.APagar, new Money(999_00),
            "aluguel", FrequenciaRecorrencia.Mensal, Referencia.AddMonths(-1), diaFixo: 1).Valor;
        await repo.SalvarAsync(doOutroTenant);

        var servico = new ContasFixasService(repo);
        var resultado = await servico.ListarAsync(BusinessId);

        Assert.Empty(resultado);
    }
}
