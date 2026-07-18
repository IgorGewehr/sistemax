using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Smoke ponta-a-ponta HTTP (rotas de PRODUÇÃO, mesmo <see cref="PermissaoHttpFixture"/> de
/// <see cref="AnalisePorProjetoHttpSmokeTests"/>) de Imobilizado + Painel de ROI do negócio
/// (docs/financeiro/design-imobilizado-roi.md): prova o gating §2.2 ANTES do toggle (leitura
/// silenciosa `[]`/404, escrita 422 `financeiro.imobilizado.desativado`), o parsing de
/// <c>Natureza</c>/<c>Categoria</c> via <c>Enum.TryParse</c> nos endpoints (string inválida ⇒
/// 400 <c>ValidationProblem</c>), o cenário nominal §4.3 (Equipamento R$12.000/60m ⇒
/// R$200,00/mês exato) via <c>POST /financeiro/imobilizado</c>, o ciclo completo de
/// <c>AporteDeCapital</c> (criar/listar/excluir) e o shape do
/// <c>GET /financeiro/roi-negocio</c> (§7.1) rodando a aplicação de ponta a ponta — não só a
/// lógica isolada em unit/InMemory test.
/// </summary>
public sealed class ImobilizadoRoiHttpSmokeTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public ImobilizadoRoiHttpSmokeTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FluxoCompleto_GatingDesligadoDepoisLigado_ImobilizadoAportesRoiRespondemComNumerosReais()
    {
        // 1) TOGGLE DESLIGADO (default) — §2.2: leitura nunca 404/erro (silenciosa), escrita 422.
        using (var listarImobilizado = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/imobilizado", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarImobilizado);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(0, corpo.GetArrayLength());
        }

        using (var listarAportes = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/aportes", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarAportes);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(0, corpo.GetArrayLength());
        }

        // GET /financeiro/roi-negocio: 404 (é um painel, não uma listagem) com o payload {erro, mensagem}.
        using (var roiDesligado = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/roi-negocio", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(roiDesligado);
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("financeiro.imobilizado.desativado", corpo.GetProperty("erro").GetString());
        }

        // POST /financeiro/imobilizado com toggle desligado ⇒ 422 financeiro.imobilizado.desativado.
        using (var criarDesligado = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/imobilizado", "founder"))
        {
            criarDesligado.Content = JsonContent.Create(new
            {
                nome = "Bancada ESD",
                natureza = "Tangivel",
                categoria = "Equipamento",
                custoAquisicaoCentavos = 1_200_000L,
                dataAquisicao = "2026-07-10",
                vidaUtilMeses = 60
            });
            using var resposta = await _fixture.Client.SendAsync(criarDesligado);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            // Payload de escrita (ParaRespostaHttp) é { codigo, mensagem } — diferente do { erro, mensagem }
            // do roi-negocio (Results.NotFound inline, é um painel, não um Result.Falhar comum).
            Assert.Equal("financeiro.imobilizado.desativado", corpo.GetProperty("codigo").GetString());
        }

        // POST /financeiro/aportes com toggle desligado ⇒ mesmo 422 (mesmo gate — §2.2).
        using (var aportarDesligado = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/aportes", "founder"))
        {
            aportarDesligado.Content = JsonContent.Create(new { valorCentavos = 2_000_000L, data = "2026-07-01", descricao = "Capital de giro inicial" });
            using var resposta = await _fixture.Client.SendAsync(aportarDesligado);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("financeiro.imobilizado.desativado", corpo.GetProperty("codigo").GetString());
        }

        // 2) LIGA O TOGGLE — imobilizadoRoiAtivo é o SEGUNDO toggle, independente de analisePorProjetoAtiva.
        using (var ligarToggle = _fixture.Requisicao(HttpMethod.Put, "/api/financeiro/configuracoes", "founder"))
        {
            ligarToggle.Content = JsonContent.Create(new
            {
                analisePorProjetoAtiva = false,
                custoHoraPadraoCentavos = (long?)null,
                tempoEntraNoDre = false,
                imobilizadoRoiAtivo = true,
                taxaDescontoAnualBps = (int?)null,
                inicioOperacao = (string?)null
            });
            using var resposta = await _fixture.Client.SendAsync(ligarToggle);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.True(corpo.GetProperty("imobilizadoRoiAtivo").GetBoolean());
        }

        // 3) Natureza/Categoria inválida via HTTP ⇒ 400 ValidationProblem (prova o Enum.TryParse do endpoint,
        //    não só a lógica de domínio).
        using (var criarInvalido = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/imobilizado", "founder"))
        {
            criarInvalido.Content = JsonContent.Create(new
            {
                nome = "Bem Inválido",
                natureza = "NaoExiste",
                categoria = "Equipamento",
                custoAquisicaoCentavos = 100_000L,
                dataAquisicao = "2026-07-10",
                vidaUtilMeses = 12
            });
            using var resposta = await _fixture.Client.SendAsync(criarInvalido);
            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        }

        // 4) POST /financeiro/imobilizado — cenário nominal §4.3: Equipamento R$12.000, 60m,
        //    residual 0 ⇒ 1.200.000 ÷ 60 = 20.000 centavos/mês EXATO (sem resto Hamilton).
        string ativoId;
        using (var criarImobilizado = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/imobilizado", "founder"))
        {
            criarImobilizado.Content = JsonContent.Create(new
            {
                nome = "Bancada ESD",
                natureza = "Tangivel",
                categoria = "Equipamento",
                custoAquisicaoCentavos = 1_200_000L,
                dataAquisicao = "2026-07-10",
                vidaUtilMeses = 60
            });
            using var resposta = await _fixture.Client.SendAsync(criarImobilizado);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Tangivel", corpo.GetProperty("natureza").GetString());
            Assert.Equal("Equipamento", corpo.GetProperty("categoria").GetString());
            Assert.Equal(20_000L, corpo.GetProperty("amortizacaoMensalCentavos").GetInt64());
            ativoId = corpo.GetProperty("id").GetString()!;
        }

        // 5) GET /financeiro/imobilizado — lista real, com o toggle ligado.
        using (var listarImobilizado = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/imobilizado", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarImobilizado);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
            Assert.Equal(ativoId, corpo[0].GetProperty("id").GetString());
        }

        // 6) Aportes: cria, lista, exclui — ciclo completo (DI5: delete físico).
        string aporteId;
        using (var criarAporte = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/aportes", "founder"))
        {
            criarAporte.Content = JsonContent.Create(new { valorCentavos = 2_000_000L, data = "2026-07-01", descricao = "Capital de giro inicial" });
            using var resposta = await _fixture.Client.SendAsync(criarAporte);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(2_000_000L, corpo.GetProperty("valorCentavos").GetInt64());
            aporteId = corpo.GetProperty("id").GetString()!;
        }

        using (var listarAportes = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/aportes", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarAportes);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
        }

        // 7) GET /financeiro/roi-negocio — o entregável central: painel real de ponta a ponta via HTTP,
        //    com um bem (capex) e um aporte já registrados (§7.1).
        using (var roiLigado = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/roi-negocio", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(roiLigado);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);

            var investimento = corpo.GetProperty("investimento");
            Assert.Equal(1_200_000L, investimento.GetProperty("capexCentavos").GetInt64());
            Assert.Equal(2_000_000L, investimento.GetProperty("aportesCentavos").GetInt64());
            Assert.Equal(3_200_000L, investimento.GetProperty("totalCentavos").GetInt64());
            Assert.Equal(1, investimento.GetProperty("bens").GetInt32());

            // Recuperação/payback/TIR/roi/serie precisam existir no shape — a prova de serialização
            // ponta-a-ponta do §7.1, não só que o campo investimento existe.
            Assert.True(corpo.TryGetProperty("recuperacao", out _));
            Assert.True(corpo.TryGetProperty("payback", out _));
            Assert.True(corpo.TryGetProperty("tir", out _));
            Assert.True(corpo.TryGetProperty("roi", out _));
            Assert.True(corpo.GetProperty("serie").GetArrayLength() > 0);
        }

        // 8) DELETE /financeiro/aportes/{id} — delete físico (DI5): 204, some da listagem.
        using (var excluirAporte = _fixture.Requisicao(HttpMethod.Delete, $"/api/financeiro/aportes/{aporteId}", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(excluirAporte);
            Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        }

        using (var listarAportesFinal = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/aportes", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarAportesFinal);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(0, corpo.GetArrayLength());
        }

        // 9) DELETE de aporte inexistente ⇒ 404 (rota real, não só o use case InMemory).
        using (var excluirInexistente = _fixture.Requisicao(HttpMethod.Delete, "/api/financeiro/aportes/nao-existe", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(excluirInexistente);
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        }

        // 10) POST /financeiro/imobilizado/{id}/baixar com valorVendaCentavos != null ⇒ alienação (I4):
        //     transiciona pra Vendido e devolve resultadoAlienacaoCentavos no DTO de fio.
        using (var venderAtivo = _fixture.Requisicao(HttpMethod.Post, $"/api/financeiro/imobilizado/{ativoId}/baixar", "founder"))
        {
            venderAtivo.Content = JsonContent.Create(new { motivo = "Upgrade de bancada", competencia = "2026-08-01", valorVendaCentavos = 1_000_000L });
            using var resposta = await _fixture.Client.SendAsync(venderAtivo);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Vendido", corpo.GetProperty("status").GetString());
            Assert.Equal(1_000_000L, corpo.GetProperty("valorVendaCentavos").GetInt64());
            Assert.True(corpo.TryGetProperty("resultadoAlienacaoCentavos", out var resultadoAlienacao) && resultadoAlienacao.ValueKind != JsonValueKind.Null);
        }
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
