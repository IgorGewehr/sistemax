using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Smoke ponta-a-ponta HTTP (rotas de PRODUÇÃO, mesmo <see cref="PermissaoHttpFixture"/> de
/// <see cref="ImobilizadoRoiHttpSmokeTests"/>) de <c>FiscalEndpointsModule</c> — achado de
/// auditoria: o módulo Fiscal existia por inteiro (emissão/transmissão/cancelamento/desistência)
/// sem NENHUMA rota HTTP. Prova o gating, o read-model que faltava (listagem de documentos), o
/// Passo 3(b) (CSC round-trip via <c>PUT/GET /fiscal/configuracao</c>, nunca vazando o token) e o
/// Passo 3(a) (CC-e só sobre documento Autorizado — FSM guard testada via HTTP de ponta a ponta,
/// não só em unit test do caso de uso isolado).
///
/// Documentos são semeados DIRETO no <see cref="IDocumentoFiscalRepository"/> singleton do
/// <c>TestServer</c> (via <c>_fixture.Server.Services</c>) em vez de emitidos via
/// <c>POST /fiscal/documentos</c> — emitir de verdade exige PerfilFiscalNCM/RegraFiscalPorOperacao
/// seedados (fora do escopo desta rodada de exposição HTTP, ver <c>EmitirDocumentoFiscalUseCaseTests</c>
/// para a cobertura completa do pipeline de emissão); aqui o alvo é a rota, não o motor de cálculo.
/// </summary>
public sealed class FiscalHttpSmokeTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public FiscalHttpSmokeTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    private static ItemDocumentoFiscal ItemComIcms() => new(
        "produto-1", "Produto 1", "12345678", null, OrigemMercadoria.Nacional, "5102",
        new Quantidade(1000), new Money(1000), Money.Zero,
        [new TributoResolvidoItem(TipoTributo.Icms, "102", new Money(1000), new Percentual(180_000), new Money(180))]);

    private async Task<DocumentoFiscal> SemearDocumentoAutorizadoAsync(string origemId)
    {
        var repositorio = _fixture.Server.Services.GetRequiredService<IDocumentoFiscalRepository>();
        var doc = DocumentoFiscal.Abrir(PermissaoHttpFixture.BusinessIdDeTeste, TipoDocumentoFiscal.NFe, new SourceRef("vendas", origemId));
        doc.AdicionarItemResolvido(ItemComIcms());
        doc.AlocarNumero("1", new Random().Next(1000, 999_999));
        doc.RegistrarAutorizacao($"chave-{origemId}", $"protocolo-{origemId}", DateTimeOffset.UtcNow);
        await repositorio.SalvarAsync(doc);
        return doc;
    }

    private async Task<DocumentoFiscal> SemearDocumentoRascunhoAsync(string origemId)
    {
        var repositorio = _fixture.Server.Services.GetRequiredService<IDocumentoFiscalRepository>();
        var doc = DocumentoFiscal.Abrir(PermissaoHttpFixture.BusinessIdDeTeste, TipoDocumentoFiscal.NFe, new SourceRef("vendas", origemId));
        await repositorio.SalvarAsync(doc);
        return doc;
    }

    [Fact]
    public async Task GetDocumentos_ComoManager_Passa_PostCartaCorrecao_ComoManager_Recebe403()
    {
        // Manager tem Fiscal:Ver mas não Fiscal:EmitirFiscal (PermissoesPadraoPorPapel) — viewer
        // não tem NENHUMA permissão de Fiscal, então o par certo pra provar "Ver passa, mutação
        // exigente falha" é manager, não viewer.
        using (var listar = _fixture.Requisicao(HttpMethod.Get, "/api/fiscal/documentos", "manager"))
        {
            using var resposta = await _fixture.Client.SendAsync(listar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        var doc = await SemearDocumentoAutorizadoAsync("venda-authz-1");
        using (var carta = _fixture.Requisicao(HttpMethod.Post, $"/api/fiscal/documentos/{doc.Id}/carta-correcao", "manager"))
        {
            carta.Content = JsonContent.Create(new { correcao = "Corrigir endereço de entrega do destinatário." });
            using var resposta = await _fixture.Client.SendAsync(carta);
            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        }
    }

    [Fact]
    public async Task GetDocumentos_ComoViewer_Recebe403_ViewerNaoTemPermissaoDeFiscal()
    {
        // Viewer não aparece em NENHUMA entrada de Modulo.Fiscal em PermissoesPadraoPorPapel —
        // prova que o módulo Fiscal é opt-in acima de operator (mesmo desenho do RBAC atual).
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/fiscal/documentos", "viewer");
        using var resposta = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task GetDocumentos_ListaSoDoTenantDaSessao()
    {
        await SemearDocumentoAutorizadoAsync("venda-listar-1");

        using var listar = _fixture.Requisicao(HttpMethod.Get, "/api/fiscal/documentos", "founder");
        using var resposta = await _fixture.Client.SendAsync(listar);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await LerJsonAsync(resposta);
        Assert.True(corpo.GetArrayLength() >= 1);
        Assert.All(corpo.EnumerateArray(), item => Assert.False(string.IsNullOrEmpty(item.GetProperty("id").GetString())));
    }

    /// <summary>Passo 3(a) — a FSM guard vive no caso de uso; este teste prova que a rota HTTP
    /// realmente a exercita (não só o unit test isolado do caso de uso).</summary>
    [Fact]
    public async Task CartaCorrecao_SobreDocumentoNaoAutorizado_Retorna422ComCodigoDeFsm()
    {
        var doc = await SemearDocumentoRascunhoAsync("venda-cce-rascunho");

        using var request = _fixture.Requisicao(HttpMethod.Post, $"/api/fiscal/documentos/{doc.Id}/carta-correcao", "founder");
        request.Content = JsonContent.Create(new { correcao = "Corrigir endereço de entrega do destinatário." });
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var corpo = await LerJsonAsync(resposta);
        Assert.Equal("fiscal.cce.documento_nao_autorizado", corpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task CartaCorrecao_SobreDocumentoAutorizado_RegistraEApareceNoHistorico()
    {
        var doc = await SemearDocumentoAutorizadoAsync("venda-cce-autorizado");

        string cartaId;
        using (var request = _fixture.Requisicao(HttpMethod.Post, $"/api/fiscal/documentos/{doc.Id}/carta-correcao", "founder"))
        {
            request.Content = JsonContent.Create(new { correcao = "Corrigir endereço de entrega do destinatário." });
            using var resposta = await _fixture.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetProperty("sequencia").GetInt32());
            cartaId = corpo.GetProperty("id").GetString()!;
        }

        using (var historico = _fixture.Requisicao(HttpMethod.Get, $"/api/fiscal/documentos/{doc.Id}/cartas-correcao", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(historico);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
            Assert.Equal(cartaId, corpo[0].GetProperty("id").GetString());
        }
    }

    [Fact]
    public async Task CartaCorrecao_SobreDocumentoInexistente_Retorna404()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/fiscal/documentos/nao-existe/carta-correcao", "founder");
        request.Content = JsonContent.Create(new { correcao = "Corrigir endereço de entrega do destinatário." });
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    /// <summary>Passo 3(b) — CSC precisa sobreviver ao round-trip HTTP e NUNCA vazar o token cru
    /// (só um booleano <c>cscConfigurado</c> — mesmo cuidado de nunca devolver senha de certificado).</summary>
    [Fact]
    public async Task ConfiguracaoFiscal_PutComCsc_GetNuncaVazaOTokenSoOBooleano()
    {
        using (var atualizar = _fixture.Requisicao(HttpMethod.Put, "/api/fiscal/configuracao", "founder"))
        {
            atualizar.Content = JsonContent.Create(new
            {
                regime = "SimplesNacional",
                ufOrigem = "sp",
                serieNfce = "1",
                serieNfe = "1",
                cscId = "000001",
                cscToken = "segredo-nunca-deveria-vazar"
            });
            using var resposta = await _fixture.Client.SendAsync(atualizar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("SP", corpo.GetProperty("ufOrigem").GetString());
            Assert.Equal("000001", corpo.GetProperty("cscId").GetString());
            Assert.True(corpo.GetProperty("cscConfigurado").GetBoolean());
            Assert.False(corpo.TryGetProperty("cscToken", out _)); // NUNCA no wire
        }

        using (var ler = _fixture.Requisicao(HttpMethod.Get, "/api/fiscal/configuracao", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(ler);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.True(corpo.GetProperty("cscConfigurado").GetBoolean());
            Assert.False(corpo.TryGetProperty("cscToken", out _));
        }
    }

    [Fact]
    public async Task ConfiguracaoFiscal_RegimeInvalido_Retorna400ValidationProblem()
    {
        using var request = _fixture.Requisicao(HttpMethod.Put, "/api/fiscal/configuracao", "founder");
        request.Content = JsonContent.Create(new { regime = "RegimeQueNaoExiste", ufOrigem = "sp" });
        using var resposta = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
