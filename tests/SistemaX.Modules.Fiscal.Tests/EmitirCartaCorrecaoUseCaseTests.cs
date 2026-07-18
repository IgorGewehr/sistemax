using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Tests.Fakes;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Passo 3(a) do plano de exposição HTTP: Carta de Correção Eletrônica só pode ser registrada
/// sobre um <see cref="DocumentoFiscal"/> <see cref="StatusDocumentoFiscal.Autorizado"/> — a GUARDA
/// de FSM vive no caso de uso (o agregado não tem, nem precisa ter, o conceito de "correção", ver
/// comentário de <see cref="CartaCorrecaoFiscal"/>). Prova também o cálculo de sequência (1, 2, 3…)
/// e o limite SEFAZ de 20 por documento.
/// </summary>
public sealed class EmitirCartaCorrecaoUseCaseTests
{
    private const string TenantId = "tenant-1";

    private static ItemDocumentoFiscal ItemComIcms() => new(
        "produto-1", "Produto 1", "12345678", null, OrigemMercadoria.Nacional, "5102",
        new Quantidade(1000), new Money(1000), Money.Zero,
        [new TributoResolvidoItem(TipoTributo.Icms, "102", new Money(1000), new Percentual(180_000), new Money(180))]);

    private sealed class Ambiente
    {
        public InMemoryDocumentoFiscalRepository Documentos { get; } = new();
        public InMemoryCartaCorrecaoFiscalRepository Cartas { get; } = new();
        public InMemoryConfiguracaoFiscalTenantRepository Configuracoes { get; } = new();

        public Ambiente()
        {
            Configuracoes.SalvarAsync(ConfiguracaoFiscalTenant.Criar(TenantId, RegimeTributario.SimplesNacional, "sp").Valor)
                .GetAwaiter().GetResult();
        }

        public EmitirCartaCorrecaoUseCase CriarUseCase(FakeGatewayCartaCorrecaoSefaz gateway) =>
            new(Documentos, Cartas, Configuracoes, gateway);

        public async Task<DocumentoFiscal> CriarDocumentoAutorizadoAsync(string origemId = "venda-1")
        {
            var doc = DocumentoFiscal.Abrir(TenantId, TipoDocumentoFiscal.NFe, new SourceRef("vendas", origemId));
            doc.AdicionarItemResolvido(ItemComIcms());
            doc.AlocarNumero("1", 1);
            doc.RegistrarAutorizacao("35260112345678000195650010000000091000000091", "135260000000001", DateTimeOffset.UtcNow);
            await Documentos.SalvarAsync(doc);
            return doc;
        }

        public async Task<DocumentoFiscal> CriarDocumentoRascunhoAsync(string origemId = "venda-2")
        {
            var doc = DocumentoFiscal.Abrir(TenantId, TipoDocumentoFiscal.NFe, new SourceRef("vendas", origemId));
            await Documentos.SalvarAsync(doc);
            return doc;
        }
    }

    [Fact]
    public async Task ExecutarAsync_DocumentoAutorizado_RegistraCorrecaoComSequencia1()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoAutorizadoAsync();
        var gateway = FakeGatewayCartaCorrecaoSefaz.Sucesso();
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(documento.Id, "Corrigir endereço de entrega do destinatário.", DateTimeOffset.UtcNow);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, resultado.Valor.Sequencia);
        Assert.Equal(documento.Id, resultado.Valor.DocumentoFiscalId);
        Assert.Equal(1, gateway.Chamadas);
        Assert.Equal(1, gateway.UltimaSequenciaRecebida);
        Assert.Equal(documento.ChaveDeAcesso, gateway.UltimaChaveRecebida);

        var persistidas = await ambiente.Cartas.ListarPorDocumentoAsync(documento.Id);
        Assert.Single(persistidas);
    }

    [Fact]
    public async Task ExecutarAsync_DuasCorrecoesSeguidas_IncrementaSequencia()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoAutorizadoAsync();
        var useCase = ambiente.CriarUseCase(FakeGatewayCartaCorrecaoSefaz.Sucesso());

        await useCase.ExecutarAsync(documento.Id, "Primeira correção do documento fiscal.", DateTimeOffset.UtcNow);
        var segunda = await useCase.ExecutarAsync(documento.Id, "Segunda correção do documento fiscal.", DateTimeOffset.UtcNow);

        Assert.True(segunda.Sucesso);
        Assert.Equal(2, segunda.Valor.Sequencia);
    }

    [Fact]
    public async Task ExecutarAsync_DocumentoNaoAutorizado_FalhaComGuardaDeFsm()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoRascunhoAsync();
        var gateway = FakeGatewayCartaCorrecaoSefaz.Sucesso();
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(documento.Id, "Corrigir endereço de entrega do destinatário.", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.cce.documento_nao_autorizado", resultado.Erro.Codigo);
        Assert.Equal(0, gateway.Chamadas); // nunca chama o gateway sem passar pela guarda de FSM
    }

    [Fact]
    public async Task ExecutarAsync_TextoCurto_FalhaAntesDeChamarGateway()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoAutorizadoAsync();
        var gateway = FakeGatewayCartaCorrecaoSefaz.Sucesso();
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(documento.Id, "curto", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.cce.texto_curto", resultado.Erro.Codigo);
        Assert.Equal(0, gateway.Chamadas);
    }

    [Fact]
    public async Task ExecutarAsync_DocumentoInexistente_Falha()
    {
        var ambiente = new Ambiente();
        var useCase = ambiente.CriarUseCase(FakeGatewayCartaCorrecaoSefaz.Sucesso());

        var resultado = await useCase.ExecutarAsync("documento-que-nao-existe", "Corrigir endereço de entrega do destinatário.", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.documento.nao_encontrado", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task ExecutarAsync_GatewayFalha_NaoPersisteCartaDeCorrecao()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoAutorizadoAsync();
        var gateway = FakeGatewayCartaCorrecaoSefaz.FalhandoInfra();
        var useCase = ambiente.CriarUseCase(gateway);

        var resultado = await useCase.ExecutarAsync(documento.Id, "Corrigir endereço de entrega do destinatário.", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Empty(await ambiente.Cartas.ListarPorDocumentoAsync(documento.Id));
    }

    [Fact]
    public async Task ExecutarAsync_Alem20Correcoes_RespeitaLimiteSefazSemChamarGateway()
    {
        var ambiente = new Ambiente();
        var documento = await ambiente.CriarDocumentoAutorizadoAsync();
        var gateway = FakeGatewayCartaCorrecaoSefaz.Sucesso();
        var useCase = ambiente.CriarUseCase(gateway);

        for (var i = 0; i < 20; i++)
            await useCase.ExecutarAsync(documento.Id, $"Correção número {i} do documento fiscal.", DateTimeOffset.UtcNow);

        var chamadasAntes = gateway.Chamadas;
        var resultado = await useCase.ExecutarAsync(documento.Id, "Vigésima primeira correção — deve ser rejeitada.", DateTimeOffset.UtcNow);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.cce.limite_excedido", resultado.Erro.Codigo);
        Assert.Equal(chamadasAntes, gateway.Chamadas); // não gastou chamada de rede numa correção que seria rejeitada
    }
}
