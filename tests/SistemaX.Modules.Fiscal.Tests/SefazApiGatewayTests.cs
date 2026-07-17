using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Domain.Comum;
using SistemaX.Modules.Fiscal.Domain.Documentos;
using SistemaX.Modules.Fiscal.Domain.Ncm;
using SistemaX.Modules.Fiscal.Domain.Produtos;
using SistemaX.Modules.Fiscal.Domain.Regimes;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Infrastructure.Sefaz;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Fiscal.Tests;

/// <summary>
/// Cobre o adapter HTTP de verdade — modo MOCK nunca toca rede (mesmo comportamento de
/// <c>isMockMode()</c> em sefaz-gateway.ts) e o tratamento de 422 como rejeição de NEGÓCIO
/// (<see cref="Result.Ok"/>, nunca <see cref="Result.Falhar{T}"/>), conforme
/// docs/fiscal/emissao-mapping.md §7.1.
/// </summary>
public sealed class SefazApiGatewayTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Modo MOCK nunca deveria fazer uma chamada de rede real.");
    }

    private static DocumentoFiscal DocumentoNumeroAlocado() => DocumentoNumeroAlocado(TipoDocumentoFiscal.NFCe);

    /// <summary>Mesma carga da NFC-e default, mas permitindo escolher o <see cref="TipoDocumentoFiscal"/>
    /// e injetar itens sob medida — usado pelos testes de NF-e/NFS-e que
    /// <see cref="DocumentoNumeroAlocado()"/> (só NFC-e) não cobre (item 1 das pendências: até aqui
    /// só NFC-e tinha cobertura indireta via este arquivo).</summary>
    private static DocumentoFiscal DocumentoNumeroAlocado(TipoDocumentoFiscal tipo, IReadOnlyList<ItemDocumentoFiscal>? itens = null)
    {
        var documento = DocumentoFiscal.Abrir("tenant-1", tipo, new SourceRef("vendas", "venda-1"));
        itens ??=
        [
            new ItemDocumentoFiscal(
                "produto-1", "Produto 1", "12345678", null, OrigemMercadoria.Nacional, "5102",
                Quantidade.DeInteiro(2), Money.DeReais(50), Money.Zero,
                [new TributoResolvidoItem(TipoTributo.Icms, "102", Money.DeReais(100), Percentual.Zero, Money.Zero)]),
        ];

        foreach (var item in itens)
            Assert.True(documento.AdicionarItemResolvido(item).Sucesso);

        Assert.True(documento.AlocarNumero("1", 1).Sucesso);
        return documento;
    }

    private sealed record GatewayComInsumos(
        SefazApiGateway Gateway,
        InMemoryConfiguracaoFiscalTenantRepository Configuracoes,
        InMemoryCadastroFiscalEmitenteRepository Emitentes,
        InMemoryCertificadoDigitalRepository Certificados,
        InMemoryDadosFiscaisProdutoCacheRepository DadosProduto,
        InMemoryReferenciaDevolucaoDocumentoFiscalRepository ReferenciasDevolucao);

    private static GatewayComInsumos CriarGateway(HttpMessageHandler handler, SefazGatewayOptions opcoes)
    {
        var configuracoes = new InMemoryConfiguracaoFiscalTenantRepository();
        var emitentes = new InMemoryCadastroFiscalEmitenteRepository();
        var certificados = new InMemoryCertificadoDigitalRepository();
        var dadosProduto = new InMemoryDadosFiscaisProdutoCacheRepository();
        var referenciasDevolucao = new InMemoryReferenciaDevolucaoDocumentoFiscalRepository();

        var gateway = new SefazApiGateway(
            new HttpClient(handler) { BaseAddress = new Uri("http://fake-sefaz-gateway.local") },
            Options.Create(opcoes),
            configuracoes,
            emitentes,
            certificados,
            new InMemoryDestinatarioDocumentoFiscalRepository(),
            new InMemoryFormaPagamentoDocumentoFiscalRepository(),
            dadosProduto,
            referenciasDevolucao,
            NullLogger<SefazApiGateway>.Instance);

        return new GatewayComInsumos(gateway, configuracoes, emitentes, certificados, dadosProduto, referenciasDevolucao);
    }

    [Fact]
    public async Task TransmitirAsync_ModoMock_NuncaChamaRedeEDevolveAutorizado()
    {
        var insumos = CriarGateway(new ThrowingHttpMessageHandler(), new SefazGatewayOptions { Ambiente = "mock" });

        var resultado = await insumos.Gateway.TransmitirAsync(DocumentoNumeroAlocado());

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, resultado.Valor.Status);
        Assert.NotNull(resultado.Valor.ChaveDeAcesso);
        Assert.Equal(44, resultado.Valor.ChaveDeAcesso!.Length);
        Assert.NotNull(resultado.Valor.Protocolo);
    }

    // Item 1 das pendências: o mock, por design, sempre devolvia Autorizado — não existia um
    // teste que combinasse literalmente "modo mock" + "rejeição" num único caminho (o teste de
    // 422 abaixo roda contra o HTTP real via StubHttpMessageHandler, não contra ModoMock). Os dois
    // testes a seguir provam Rejeitado/Denegado SEM I/O algum, via SefazGatewayOptions.MockDesfecho.
    [Theory]
    [InlineData("rejeitado", StatusDocumentoFiscal.Rejeitado)]
    [InlineData("denegado", StatusDocumentoFiscal.Denegado)]
    public async Task TransmitirAsync_ModoMock_ComDesfechoForcado_DevolveDesfechoSemIO(
        string mockDesfecho, StatusDocumentoFiscal statusEsperado)
    {
        var insumos = CriarGateway(new ThrowingHttpMessageHandler(), new SefazGatewayOptions
        {
            Ambiente = "mock",
            MockDesfecho = mockDesfecho,
            MockMotivoDesfecho = "Motivo de teste",
        });

        var resultado = await insumos.Gateway.TransmitirAsync(DocumentoNumeroAlocado());

        // ThrowingHttpMessageHandler lançaria se qualquer I/O de rede fosse tentado — o próprio
        // Assert.True(resultado.Sucesso) abaixo já prova que isso não aconteceu.
        Assert.True(resultado.Sucesso);
        Assert.Equal(statusEsperado, resultado.Valor.Status);
        Assert.Equal("Motivo de teste", resultado.Valor.Motivo);
        Assert.Null(resultado.Valor.ChaveDeAcesso);
    }

    [Fact]
    public async Task TransmitirAsync_SemConfiguracaoTenant_FalhaDeInfraNomeada()
    {
        // Fora do modo mock, sem ConfiguracaoFiscalTenant seedada — falha nomeada, nunca dado
        // inventado (mesmo princípio de MotorDeCalculoTributario, agora no lado do gateway).
        var insumos = CriarGateway(new ThrowingHttpMessageHandler(), new SefazGatewayOptions { Ambiente = "homologacao" });

        var resultado = await insumos.Gateway.TransmitirAsync(DocumentoNumeroAlocado());

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.emissao.configuracao_tenant_ausente", resultado.Erro.Codigo);
    }

    [Fact]
    public async Task TransmitirAsync_Resposta422_DevolveRejeitadoComoResultadoOk()
    {
        const string corpo = """
            { "success": false, "status": "rejeitado", "codigoStatus": "225", "motivoStatus": "Rejeição 225: falha no schema XML" }
            """;

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode)422)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });

        var documento = DocumentoNumeroAlocado();
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        // 422 é resposta de NEGÓCIO — nunca Result.Falhar (contrato explícito de IGatewayEmissaoSefaz).
        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Rejeitado, resultado.Valor.Status);
        Assert.Contains("225", resultado.Valor.Motivo);
    }

    [Fact]
    public async Task TransmitirAsync_401_PropagaFalhaImediatamenteSemRetry()
    {
        var tentativas = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            tentativas++;
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"token invalido"}""", Encoding.UTF8, "application/json"),
            };
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao", MaxRetries = 3 });
        var documento = DocumentoNumeroAlocado();
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Falha);
        Assert.Equal("fiscal.sefaz.401", resultado.Erro.Codigo);
        Assert.Equal(1, tentativas); // 401 nunca retenta (§2/§7.1)
    }

    [Fact]
    public async Task TransmitirAsync_503ComRetryDepoisSucesso_Autoriza()
    {
        var tentativas = 0;
        const string corpoSucesso = """
            { "success": true, "status": "autorizado", "chaveAcesso": "35260112345678000195650010000000091000000091", "protocolo": "135260000000001" }
            """;

        var handler = new StubHttpMessageHandler(_ =>
        {
            tentativas++;
            if (tentativas < 2)
                return new HttpResponseMessage((HttpStatusCode)503) { Content = new StringContent("indisponível") };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(corpoSucesso, Encoding.UTF8, "application/json"),
            };
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao", MaxRetries = 3, TimeoutSeconds = 5 });
        var documento = DocumentoNumeroAlocado();
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, resultado.Valor.Status);
        Assert.Equal(2, tentativas);
    }

    [Fact]
    public async Task CancelarAsync_DocumentoComProtocoloDaAutorizacaoOriginal_EnviaProtocoloNoPayload()
    {
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "success": true, "status": "cancelado", "protocolo": "135260000000999" }""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado();
        await SemearInsumosAsync(insumos, documento.TenantId);
        documento.RegistrarAutorizacao("35260112345678000195650010000000091000000091", "135260000000123", DateTimeOffset.UtcNow);

        var resultado = await insumos.Gateway.CancelarAsync(documento, "Cliente desistiu da compra hoje");

        Assert.True(resultado.Sucesso);
        Assert.NotNull(corpoEnviado);
        Assert.Contains("\"protocolo\":\"135260000000123\"", corpoEnviado);
    }

    // ------------------------------------------------------------------ item 1 das pendências:
    // MontarNFe/MontarNFSe (e os ramos NFe/NFSe de SefazApiGateway.MontarPayload) não tinham
    // NENHUM teste — só MontarNFCe era exercitado indiretamente pelos testes acima. Os testes a
    // seguir fecham essa lacuna passando pelo MESMO caminho público (TransmitirAsync), sem tocar o
    // mapper internal diretamente.

    [Fact]
    public async Task TransmitirAsync_NFe_ChamaEndpointNfeEMontaEnvelopeDeVendaDeMercadoria()
    {
        string? endpointChamado = null;
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            endpointChamado = request.RequestUri!.AbsolutePath;
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "success": true, "status": "autorizado", "chaveAcesso": "35260112345678000195550010000000011000000011", "protocolo": "135260000000001" }""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFe);
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusDocumentoFiscal.Autorizado, resultado.Valor.Status);
        Assert.Equal("/nfe/emitir", endpointChamado);
        Assert.NotNull(corpoEnviado);

        using var json = JsonDocument.Parse(corpoEnviado!);
        var raiz = json.RootElement;
        Assert.Equal("VENDA DE MERCADORIA", PropriedadeCI(raiz, "naturezaOperacao").GetString());
        Assert.Equal("9", PropriedadeCI(raiz, "presencaComprador").GetString());
        Assert.Equal(1, PropriedadeCI(raiz, "numero").GetInt64());
    }

    [Fact]
    public async Task TransmitirAsync_NFSe_ChamaEndpointNfseEMontaPrestadorETomador()
    {
        string? endpointChamado = null;
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            endpointChamado = request.RequestUri!.AbsolutePath;
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "success": true, "status": "autorizado", "chaveAcesso": "35260112345678000195550010000000011000000022", "protocolo": "135260000000002" }""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var itens = new List<ItemDocumentoFiscal>
        {
            new(
                "servico-1", "Serviço de consultoria", "00000000", null, OrigemMercadoria.Nacional, "9101",
                Quantidade.DeInteiro(1), Money.DeReais(200), Money.Zero,
                [
                    new TributoResolvidoItem(TipoTributo.Icms, "102", Money.Zero, Percentual.Zero, Money.Zero),
                    new TributoResolvidoItem(TipoTributo.Iss, null, Money.DeReais(200), Percentual.DePorcentagem(5), Money.DeReais(10)),
                ]),
        };

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFSe, itens);
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.Equal("/nfse/emitir", endpointChamado);
        Assert.NotNull(corpoEnviado);

        using var json = JsonDocument.Parse(corpoEnviado!);
        var raiz = json.RootElement;
        var prestador = PropriedadeCI(raiz, "prestador");
        Assert.Equal("Empresa Teste LTDA", PropriedadeCI(prestador, "nome").GetString());
        var issqn = PropriedadeCI(raiz, "issqn");
        Assert.Equal(5m, PropriedadeCI(issqn, "aliquota").GetDecimal());
        Assert.Equal(10m, PropriedadeCI(issqn, "valorISS").GetDecimal());
    }

    [Fact]
    public async Task TransmitirAsync_ComReferenciaDevolucaoVinculada_IncluiRefNFeNoPayload()
    {
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{ "success": true, "status": "autorizado", "chaveAcesso": "35260112345678000195550010000000011000000033", "protocolo": "135260000000003" }""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFe);
        await SemearInsumosAsync(insumos, documento.TenantId);
        await insumos.ReferenciasDevolucao.VincularAsync(documento.Id, "35260112345678000195550010000000011000000099");

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(corpoEnviado);
        using var json = JsonDocument.Parse(corpoEnviado!);
        var referencias = PropriedadeCI(json.RootElement, "referencias");
        Assert.Equal("35260112345678000195550010000000011000000099", PropriedadeCI(referencias, "refNFe").GetString());
    }

    [Fact]
    public async Task TransmitirAsync_SemCacheDeProduto_ItemUsaFallbackSemGtinEUnidadeUn()
    {
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return RespostaAutorizadoPadrao();
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFe);
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(corpoEnviado);
        using var json = JsonDocument.Parse(corpoEnviado!);
        var item = PropriedadeCI(json.RootElement, "produtos")[0];
        Assert.Equal("SEM GTIN", PropriedadeCI(item, "cean").GetString());
        Assert.Equal("UN", PropriedadeCI(item, "unidade").GetString());
    }

    [Fact]
    public async Task TransmitirAsync_ComCacheDeProduto_ItemUsaGtinEUnidadeReais()
    {
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return RespostaAutorizadoPadrao();
        });

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFe);
        await SemearInsumosAsync(insumos, documento.TenantId);
        await insumos.DadosProduto.SalvarAsync(new DadosFiscaisProdutoCache(
            documento.TenantId, "produto-1", null, null, NaturezaOperacaoProduto.RevendaDeTerceiros, null,
            Gtin: "7891234567895", UnidadeComercial: "CX"));

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(corpoEnviado);
        using var json = JsonDocument.Parse(corpoEnviado!);
        var item = PropriedadeCI(json.RootElement, "produtos")[0];
        Assert.Equal("7891234567895", PropriedadeCI(item, "cean").GetString());
        Assert.Equal("CX", PropriedadeCI(item, "unidade").GetString());
    }

    /// <summary>Gap #7 (emissao-mapping.md §4.4/§11) — <c>pICMSInter</c> tem que refletir a
    /// alíquota interestadual EFETIVAMENTE usada no ICMS do item (aqui 4%, regra da Resolução do
    /// Senado 13/2012 para mercadoria importada — <see cref="OrigemMercadoria.EstrangeiraImportacaoDireta"/>),
    /// nunca um valor fixo de 12% independente do item.</summary>
    [Fact]
    public async Task TransmitirAsync_ComDifalEFcp_IcmsUfDestUsaAliquotaRealDoItemNuncaHardcoded()
    {
        string? corpoEnviado = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            corpoEnviado = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return RespostaAutorizadoPadrao();
        });

        var itens = new List<ItemDocumentoFiscal>
        {
            new(
                "produto-importado", "Produto Importado", "84713012", null,
                OrigemMercadoria.EstrangeiraImportacaoDireta, "6108",
                Quantidade.DeInteiro(1), Money.DeReais(1000), Money.Zero,
                [
                    new TributoResolvidoItem(TipoTributo.Icms, "00", Money.DeReais(1000), Percentual.DePorcentagem(4), Money.DeReais(40)),
                    new TributoResolvidoItem(TipoTributo.IcmsDifal, null, Money.DeReais(1000), Percentual.DePorcentagem(14), Money.DeReais(140)),
                    new TributoResolvidoItem(TipoTributo.Fcp, null, Money.DeReais(1000), Percentual.DePorcentagem(2), Money.DeReais(20)),
                ]),
        };

        var insumos = CriarGateway(handler, new SefazGatewayOptions { Ambiente = "homologacao" });
        var documento = DocumentoNumeroAlocado(TipoDocumentoFiscal.NFe, itens);
        await SemearInsumosAsync(insumos, documento.TenantId);

        var resultado = await insumos.Gateway.TransmitirAsync(documento);

        Assert.True(resultado.Sucesso);
        Assert.NotNull(corpoEnviado);
        using var json = JsonDocument.Parse(corpoEnviado!);
        var item = PropriedadeCI(json.RootElement, "produtos")[0];
        var imposto = PropriedadeCI(item, "imposto");
        var icmsUfDest = PropriedadeCI(imposto, "icmsUFDest");

        Assert.Equal(4m, PropriedadeCI(icmsUfDest, "pICMSInter").GetDecimal()); // aliquota real do item — nunca 12 fixo
        Assert.Equal(14m, PropriedadeCI(icmsUfDest, "pICMSUFDest").GetDecimal());
        Assert.Equal(2m, PropriedadeCI(icmsUfDest, "pFCPUFDest").GetDecimal());
        Assert.Equal(20m, PropriedadeCI(icmsUfDest, "vFCPUFDest").GetDecimal());
    }

    private static HttpResponseMessage RespostaAutorizadoPadrao() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            """{ "success": true, "status": "autorizado", "chaveAcesso": "35260112345678000195550010000000011000000044", "protocolo": "135260000000004" }""",
            Encoding.UTF8, "application/json"),
    };

    /// <summary>Busca uma propriedade JSON por nome sem depender da regra exata de
    /// <see cref="JsonNamingPolicy.CamelCase"/> pra acrônimos (ex.: <c>CEAN</c> vira <c>cEAN</c>,
    /// <c>PICMSInter</c> vira <c>pICMSInter</c>) — os testes deste arquivo verificam O VALOR
    /// contido no payload, não a grafia exata da chave.</summary>
    private static JsonElement PropriedadeCI(JsonElement elemento, string nome)
    {
        foreach (var propriedade in elemento.EnumerateObject())
        {
            if (string.Equals(propriedade.Name, nome, StringComparison.OrdinalIgnoreCase))
                return propriedade.Value;
        }

        throw new InvalidOperationException($"Propriedade '{nome}' não encontrada no JSON: {elemento.GetRawText()}");
    }

    /// <summary>Semeia os 3 insumos obrigatórios (config/emitente/certificado) usando os MESMOS
    /// adapters InMemory que <see cref="CriarGateway"/> injetou no gateway — necessário para
    /// qualquer cenário fora do modo MOCK chegar até a montagem do payload.</summary>
    private static async Task SemearInsumosAsync(GatewayComInsumos insumos, string tenantId)
    {
        var configuracao = ConfiguracaoFiscalTenant.Criar(tenantId, RegimeTributario.SimplesNacional, "SP");
        Assert.True(configuracao.Sucesso);
        await insumos.Configuracoes.SalvarAsync(configuracao.Valor);

        await insumos.Emitentes.SalvarAsync(new CadastroFiscalEmitente(
            tenantId, "00000000000191", "Empresa Teste LTDA", "Empresa Teste",
            "123456789", null, "Rua Teste", "100", null, "Centro",
            "3550308", "São Paulo", "01000000", null));

        await insumos.Certificados.SalvarAsync(tenantId, new CertificadoDigital("cGZ4LWZha2U=", "senha-fake"));
    }
}
