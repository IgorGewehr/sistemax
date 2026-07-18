using SistemaX.Modules.Financeiro.Application.Projetos;
using SistemaX.Modules.Financeiro.Application.Tempo;
using SistemaX.Modules.Financeiro.Domain.Assinaturas;
using SistemaX.Modules.Financeiro.Domain.Configuracao;
using SistemaX.Modules.Financeiro.Domain.Projetos;
using SistemaX.Modules.Financeiro.Domain.Recorrencia;
using SistemaX.Modules.Financeiro.Infrastructure.InMemory;
using SistemaX.Modules.Financeiro.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Tests.Tempo;

/// <summary>P4 — <see cref="RegistrarApontamentoUseCase"/> (gating + derivação via assinatura) e
/// <see cref="ResumoDeTempoService"/> (soma de minutos por cliente/projeto — o índice de gargalo
/// desta fatia, design §9.7).</summary>
public sealed class ApontamentoDeTempoUseCasesTests
{
    private const string Biz = "loja-1";
    private static readonly DateTimeOffset Agora = new(2026, 7, 17, 14, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public async Task ExecutarAsync_ComToggleDesligado_Falha422()
    {
        var apontamentos = new InMemoryApontamentoDeTempoRepository();
        var assinaturas = new InMemoryAssinaturaRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        var useCase = new RegistrarApontamentoUseCase(apontamentos, assinaturas, configuracoes, new FakeRelogio(Agora));

        var resultado = await useCase.ExecutarAsync(new RegistrarApontamentoComando(Biz, 30, Agora, "op-1", "Igor", ClienteId: "cliente-1"));

        Assert.True(resultado.Falha);
        Assert.Equal("financeiro.projetos.desativado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarAsync_ComAssinaturaId_DerivaProjetoEClienteDaAssinatura()
    {
        var apontamentos = new InMemoryApontamentoDeTempoRepository();
        var assinaturas = new InMemoryAssinaturaRepository();
        var configuracoes = new InMemoryConfiguracaoFinanceiraTenantRepository();
        await configuracoes.SalvarAsync(ConfiguracaoFinanceiraTenant.Criar(Biz, analisePorProjetoAtiva: true).Valor);

        var assinatura = Assinatura.Criar(
            Biz, "cliente-1", "Empresa X", "servico-digisat", "Licença DigiSat", Money.DeReais(280),
            FrequenciaRecorrencia.Mensal, diaCobranca: 5, dataInicio: Agora.AddMonths(-1), projetoId: "projeto-digisat").Valor;
        await assinaturas.SalvarAsync(assinatura);

        var useCase = new RegistrarApontamentoUseCase(apontamentos, assinaturas, configuracoes, new FakeRelogio(Agora));
        var resultado = await useCase.ExecutarAsync(new RegistrarApontamentoComando(Biz, 30, Agora, "op-1", "Igor", AssinaturaId: assinatura.Id));

        Assert.True(resultado.Sucesso);
        Assert.Equal("projeto-digisat", resultado.Valor.ProjetoId);
        Assert.Equal("cliente-1", resultado.Valor.ClienteId);
        Assert.Equal("Empresa X", resultado.Valor.ClienteNome);
    }

    [Fact]
    public async Task ResumoDeTempoService_SomaMinutosPorClienteEProjeto()
    {
        var apontamentos = new InMemoryApontamentoDeTempoRepository();
        var projetos = new InMemoryProjetoRepository();

        var projeto = Projeto.Criar(Biz, "DigiSat", null, Agora.AddMonths(-3)).Valor;
        await projetos.SalvarAsync(projeto);

        await apontamentos.SalvarAsync(ApontamentoDeTempoComVinculo(30, "cliente-1", projeto.Id));
        await apontamentos.SalvarAsync(ApontamentoDeTempoComVinculo(45, "cliente-1", projeto.Id));
        await apontamentos.SalvarAsync(ApontamentoDeTempoComVinculo(20, "cliente-2", projeto.Id));

        var servico = new ResumoDeTempoService(apontamentos, projetos);
        var resultado = await servico.CalcularAsync(Biz, Agora.AddDays(-1), Agora.AddDays(1));

        Assert.Equal(95, resultado.MinutosTotais);
        Assert.Null(resultado.CustoTotalCentavos); // decisão do dono — sempre null nesta fatia

        var porProjeto = Assert.Single(resultado.PorProjeto);
        Assert.Equal("DigiSat", porProjeto.ProjetoNome);
        Assert.Equal(95, porProjeto.Minutos);

        Assert.Equal(2, resultado.PorCliente.Count);
        // Ordenado por minutos desc — o índice de gargalo desta fatia.
        Assert.Equal("cliente-1", resultado.PorCliente[0].ClienteId);
        Assert.Equal(75, resultado.PorCliente[0].Minutos);
        Assert.Equal("cliente-2", resultado.PorCliente[1].ClienteId);
        Assert.Equal(20, resultado.PorCliente[1].Minutos);
    }

    [Fact]
    public async Task ExcluirApontamentoUseCase_RemoveFisicamenteSemFsm()
    {
        var apontamentos = new InMemoryApontamentoDeTempoRepository();
        var apontamento = ApontamentoDeTempoComVinculo(30, "cliente-1", null);
        await apontamentos.SalvarAsync(apontamento);

        var useCase = new ExcluirApontamentoUseCase(apontamentos);
        var excluido = await useCase.ExecutarAsync(Biz, apontamento.Id);

        Assert.True(excluido);
        Assert.Null(await apontamentos.ObterPorIdAsync(Biz, apontamento.Id));
    }

    private static Domain.Tempo.ApontamentoDeTempo ApontamentoDeTempoComVinculo(int minutos, string clienteId, string? projetoId)
        => Domain.Tempo.ApontamentoDeTempo.Criar(
            Biz, minutos, Agora, "op-1", "Igor", Agora, projetoId: projetoId, clienteId: clienteId, clienteNome: "Empresa X").Valor;
}
