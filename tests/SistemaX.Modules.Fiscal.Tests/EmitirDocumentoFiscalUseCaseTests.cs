using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Application.Cfop;
using SistemaX.Modules.Fiscal.Application.Motor;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Operacoes;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Domain.Regras;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Prova o invariante que docs/fiscal/emissao-mapping.md §7.3 nomeia como o gap a fechar: depois
/// de <see cref="DocumentoFiscal.AlocarNumero"/>, o caso de uso chama de verdade
/// <c>IGatewayEmissaoSefaz.TransmitirAsync</c> e registra o desfecho na FSM — nunca mais para em
/// <c>NumeroAlocado</c> por definição de escopo (como a versão anterior desta classe fazia).
/// </summary>
public sealed class EmitirDocumentoFiscalUseCaseTests
{
    private const string TenantId = "tenant-1";
    private const string Ncm = "12345678";

    private static OperacaoFiscal OperacaoInterna() => new(
        TipoOperacaoFiscal.VendaMercadoria, "SP", "SP",
        DestinatarioConsumidorFinal: true, DestinatarioContribuinteIcms: false, OperacaoPresencial: true);

    private sealed class Ambiente
    {
        public InMemoryDocumentoFiscalRepository Documentos { get; } = new();
        public InMemorySequenciaFiscalRepository Sequencias { get; } = new();
        public InMemoryPerfilFiscalNcmRepository Perfis { get; } = new();
        public InMemoryTributacaoProdutoRepository Overrides { get; } = new();
        public InMemoryRegraFiscalPorOperacaoRepository Regras { get; } = new();
        public InMemoryDadosFiscaisProdutoCacheRepository CacheProduto { get; } = new();
        public InMemoryRegraCfopRepository RegraCfop { get; } = new();
        public FakeIntegrationEventBus Bus { get; } = new();
        public UnidadeDeTrabalhoFiscalEmMemoria UnidadeDeTrabalho { get; } = new();

        public EmitirDocumentoFiscalUseCase CriarUseCase(SistemaX.Modules.Fiscal.Application.Ports.IGatewayEmissaoSefaz gateway)
        {
            var resolvedorDeCfop = new ResolvedorDeCfop(CacheProduto, RegraCfop);
            var resolvedor = new ResolvedorDeItemFiscalService(Perfis, Overrides, Regras, resolvedorDeCfop);
            var transmissor = new TransmitirDocumentoFiscalUseCase(Documentos, Bus, gateway);
            return new EmitirDocumentoFiscalUseCase(Documentos, Sequencias, resolvedor, Bus, UnidadeDeTrabalho, transmissor);
        }

        public async Task SemearConfiguracaoSimplesNacionalAsync()
        {
            var perfil = PerfilFiscalNCM.Criar(
                TenantId, RegimeTributario.SimplesNacional, Ncm, OrigemMercadoria.Nacional,
                exigeIcmsSt: false, cest: null, aliquotaIpi: null,
                cstOuCsosnPisCofins: "07", aliquotaPis: null, aliquotaCofins: null).Valor;
            await Perfis.SalvarAsync(perfil);

            var regra = new RegraFiscalPorOperacao(
                TenantId: null, Regime: RegimeTributario.SimplesNacional, TipoOperacao: TipoOperacaoFiscal.VendaMercadoria,
                UfOrigem: "SP", UfDestino: null, IndicadorSt: false,
                SituacaoIcms: SituacaoTributariaIcms.ParaCsosn(RegimeTributario.SimplesNacional, "102").Valor,
                AliquotaInterna: null, AliquotaInterestadual: null);
            await Regras.SalvarAsync(regra);
        }
    }

    private static IReadOnlyList<ItemParaEmitir> ItemDeVendaSimples() =>
    [
        new ItemParaEmitir(
            "produto-1", "Produto 1", Ncm, Quantidade.DeInteiro(2), Money.DeReais(50), Money.Zero,
            CfopDaEmissao: "5102") // override explícito — dispensa seed de IRegraCfopRepository (§ resolução em cascata)
    ];

    [Fact]
    public async Task ExecutarAsync_GatewayAutoriza_DocumentoFicaAutorizadoComChaveEProtocolo()
    {
        var ambiente = new Ambiente();
        await ambiente.SemearConfiguracaoSimplesNacionalAsync();
        var gateway = FakeGatewayEmissaoSefaz.Autorizando("35260112345678000195650010000000091000000091", "135260000000123");
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(
            TenantId, TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "venda-1"),
            RegimeTributario.SimplesNacional, OperacaoInterna(), modelo: "65", serie: "1",
            ItemDeVendaSimples());

        Assert.True(resultado.Sucesso);
        var documento = resultado.Valor;
        Assert.Equal(StatusDocumentoFiscal.Autorizado, documento.Status);
        Assert.Equal(1L, documento.Numero);
        Assert.Equal("35260112345678000195650010000000091000000091", documento.ChaveDeAcesso);
        Assert.Equal("135260000000123", documento.Protocolo);
        Assert.Equal(1, gateway.Chamadas);
        Assert.NotNull(gateway.DocumentoRecebido);
        Assert.Equal(1L, gateway.DocumentoRecebido!.Numero); // gateway recebeu o documento JÁ com o número alocado

        // persistido — reler do repositório confirma que o SalvarAsync pós-transmissão aconteceu
        var relido = await ambiente.Documentos.ObterPorIdAsync(documento.Id);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, relido!.Status);
    }

    [Fact]
    public async Task ExecutarAsync_GatewayRejeita_DocumentoFicaRejeitadoComMotivo()
    {
        var ambiente = new Ambiente();
        await ambiente.SemearConfiguracaoSimplesNacionalAsync();
        var gateway = FakeGatewayEmissaoSefaz.Rejeitando("Rejeição 225: falha no schema XML");
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(
            TenantId, TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "venda-2"),
            RegimeTributario.SimplesNacional, OperacaoInterna(), modelo: "65", serie: "1",
            ItemDeVendaSimples());

        Assert.True(resultado.Sucesso); // 422 é resposta de negócio — nunca falha do caso de uso (§7.1)
        var documento = resultado.Valor;
        Assert.Equal(StatusDocumentoFiscal.Rejeitado, documento.Status);
        Assert.Null(documento.ChaveDeAcesso);
        Assert.Equal("Rejeição 225: falha no schema XML", documento.MotivoBloqueioOuRejeicaoOuDenegacao);

        var relido = await ambiente.Documentos.ObterPorIdAsync(documento.Id);
        Assert.Equal(StatusDocumentoFiscal.Rejeitado, relido!.Status);
    }

    [Fact]
    public async Task ExecutarAsync_GatewayFalhaPorInfra_DocumentoPermaneceNumeroAlocado()
    {
        var ambiente = new Ambiente();
        await ambiente.SemearConfiguracaoSimplesNacionalAsync();
        var gateway = FakeGatewayEmissaoSefaz.FalhandoInfra();
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(
            TenantId, TipoDocumentoFiscal.NFCe, new SourceRef("vendas", "venda-3"),
            RegimeTributario.SimplesNacional, OperacaoInterna(), modelo: "65", serie: "1",
            ItemDeVendaSimples());

        // Falha de infra do gateway NUNCA propaga como falha deste caso de uso — o número já
        // está comprometido e válido; só a transmissão falhou, retentável depois (§7.3).
        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.NumeroAlocado, resultado.Valor.Status);
        Assert.Null(resultado.Valor.ChaveDeAcesso);
    }
}
