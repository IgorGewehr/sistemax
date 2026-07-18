using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Smoke ponta-a-ponta HTTP (rotas de PRODUÇÃO, mesmo <see cref="PermissaoHttpFixture"/> de
/// <see cref="ImobilizadoRoiHttpSmokeTests"/>) de <c>ComprasEndpointsModule</c> — achado de
/// auditoria: Compras (fornecedores + notas de compra) tinha domínio+casos de uso completos sem
/// NENHUMA rota HTTP. Prova o gating (<c>Acao.Ver</c>/<c>Acao.Editar</c>), o read-model que faltava
/// (listagem de fornecedores/notas) e o fluxo completo cadastrar → registrar entrada → confirmar
/// recebimento, rodando a aplicação de ponta a ponta.
/// </summary>
public sealed class ComprasHttpSmokeTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public ComprasHttpSmokeTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PostFornecedores_ComoViewer_Recebe403_SemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/compras/fornecedores", "viewer");
        request.Content = JsonContent.Create(new { razaoSocial = "Fornecedor Teste" });

        using var resposta = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task FluxoCompleto_CadastrarFornecedor_RegistrarNota_ConfirmarRecebimento()
    {
        // 1) GET /compras/fornecedores — lista vazia antes de qualquer cadastro (read-model novo).
        using (var listar = _fixture.Requisicao(HttpMethod.Get, "/api/compras/fornecedores", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // 2) POST /compras/fornecedores — cadastra.
        string fornecedorId;
        using (var criar = _fixture.Requisicao(HttpMethod.Post, "/api/compras/fornecedores", "founder"))
        {
            criar.Content = JsonContent.Create(new { razaoSocial = "Distribuidora ACME Ltda", documento = "12345678000199" });
            using var resposta = await _fixture.Client.SendAsync(criar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Distribuidora ACME Ltda", corpo.GetProperty("razaoSocial").GetString());
            fornecedorId = corpo.GetProperty("id").GetString()!;
        }

        // 3) Idempotência (R3) — reenviar o MESMO documento devolve o fornecedor já existente, nunca duplica.
        using (var duplicado = _fixture.Requisicao(HttpMethod.Post, "/api/compras/fornecedores", "founder"))
        {
            duplicado.Content = JsonContent.Create(new { razaoSocial = "Nome Diferente Mas Mesmo CNPJ", documento = "12345678000199" });
            using var resposta = await _fixture.Client.SendAsync(duplicado);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(fornecedorId, corpo.GetProperty("id").GetString());
        }

        // 4) GET /compras/fornecedores — agora lista 1 (dedupe confirmado no passo 3).
        using (var listar = _fixture.Requisicao(HttpMethod.Get, "/api/compras/fornecedores", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listar);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
        }

        // 5) POST /compras/notas — registra entrada com 1 item já com produto conhecido (match Manual).
        string notaId;
        using (var registrar = _fixture.Requisicao(HttpMethod.Post, "/api/compras/notas", "founder"))
        {
            registrar.Content = JsonContent.Create(new
            {
                lojaId = "loja-1",
                origem = "Manual",
                numero = "1001",
                serie = "1",
                dataEmissao = DateTimeOffset.UtcNow,
                fornecedorId,
                chaveDeAcessoBruta = (string?)null,
                vProdCentavos = 10_000L,
                vNfCentavos = 10_000L,
                itens = new[]
                {
                    new
                    {
                        nItem = 1, cProd = "CPROD-1", descricaoNf = "Item 1", ncm = "12345678", unidadeNf = "UN",
                        quantidadeNfMilesimos = 1000L, vProdCentavos = 10_000L, produtoIdConhecido = "produto-1",
                        fatorConversaoConhecidoMilesimos = 1000L
                    }
                }
            });
            using var resposta = await _fixture.Client.SendAsync(registrar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("EmConferencia", corpo.GetProperty("status").GetString());
            notaId = corpo.GetProperty("id").GetString()!;
        }

        // 6) GET /compras/notas — lista 1 nota.
        using (var listar = _fixture.Requisicao(HttpMethod.Get, "/api/compras/notas", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listar);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
            Assert.Equal(notaId, corpo[0].GetProperty("id").GetString());
        }

        // 7) POST /compras/notas/{id}/confirmar-recebimento — fecha o pipeline.
        using (var confirmar = _fixture.Requisicao(HttpMethod.Post, $"/api/compras/notas/{notaId}/confirmar-recebimento", "founder"))
        {
            confirmar.Content = JsonContent.Create(new { usuarioId = "user-1", usuarioNome = "Operador" });
            using var resposta = await _fixture.Client.SendAsync(confirmar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Recebida", corpo.GetProperty("status").GetString());
        }

        // 8) GET /compras/notas/{id} — detalhe reflete o status final.
        using (var detalhe = _fixture.Requisicao(HttpMethod.Get, $"/api/compras/notas/{notaId}", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(detalhe);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Recebida", corpo.GetProperty("status").GetString());
            Assert.Single(corpo.GetProperty("itens").EnumerateArray());
        }

        // 9) GET de nota inexistente ⇒ 404 (rota real, não só o repositório InMemory).
        using (var inexistente = _fixture.Requisicao(HttpMethod.Get, "/api/compras/notas/nao-existe", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(inexistente);
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
