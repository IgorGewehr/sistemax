using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Smoke ponta-a-ponta HTTP (rotas de PRODUÇÃO, mesmo <see cref="PermissaoHttpFixture"/> de
/// <see cref="ImobilizadoRoiHttpSmokeTests"/>) de <c>AssistenciaEndpointsModule</c> — achado de
/// auditoria: o vertical Assistência Técnica (Ordem de Serviço) existia por inteiro (agregado + FSM
/// completa + casos de uso de gestão/faturamento) sem NENHUMA rota HTTP, sob
/// <c>Autorizacao.Modulo.Ordens</c>. Prova o gating e o fluxo abrir → diagnóstico → orçamento →
/// aprovação → execução → entrega rodando a aplicação de ponta a ponta via HTTP.
/// </summary>
public sealed class AssistenciaHttpSmokeTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public AssistenciaHttpSmokeTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PostOrdens_ComoViewer_Recebe403_SemPermissaoDeEditar()
    {
        using var request = _fixture.Requisicao(HttpMethod.Post, "/api/assistencia/ordens", "viewer");
        request.Content = JsonContent.Create(new
        {
            clienteId = "cliente-1", clienteNome = "Pedro Lima", equipamentoTipo = "Console",
            equipamentoMarca = "Sony", equipamentoModelo = "PS5", defeitoRelatado = "desliga sozinho"
        });

        using var resposta = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task GetOrdens_ComoViewer_Passa_ListaVaziaOuComOrdensDeOutrosTestes()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/assistencia/ordens", "viewer");
        using var resposta = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task FluxoCompleto_AbrirDiagnosticarOrcarAprovarExecutarEntregar()
    {
        // 1) POST /assistencia/ordens — abre a OS (número gerado pela camada HTTP).
        string osId;
        using (var abrir = _fixture.Requisicao(HttpMethod.Post, "/api/assistencia/ordens", "founder"))
        {
            abrir.Content = JsonContent.Create(new
            {
                clienteId = "cliente-1", clienteNome = "Pedro Lima", clienteTelefone = "(11) 98877-1234",
                equipamentoTipo = "Console", equipamentoMarca = "Sony", equipamentoModelo = "PS5 CFI-1214A",
                numeroSerie = "X1234", defeitoRelatado = "desliga sozinho após 10 min de jogo"
            });
            using var resposta = await _fixture.Client.SendAsync(abrir);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Aberta", corpo.GetProperty("status").GetString());
            Assert.StartsWith("OS-", corpo.GetProperty("numero").GetString());
            osId = corpo.GetProperty("id").GetString()!;
        }

        // 2) GET /assistencia/ordens — read-model novo, a OS aberta aparece na fila.
        using (var listar = _fixture.Requisicao(HttpMethod.Get, "/api/assistencia/ordens", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listar);
            var corpo = await LerJsonAsync(resposta);
            Assert.Contains(corpo.EnumerateArray(), item => item.GetProperty("id").GetString() == osId);
        }

        // 3) POST .../atribuir-tecnico — pré-requisito de RegistrarDiagnostico (guarda de domínio).
        using (var atribuir = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/atribuir-tecnico", "founder"))
        {
            atribuir.Content = JsonContent.Create(new { tecnicoId = "tecnico-1", tecnicoNome = "Igor" });
            using var resposta = await _fixture.Client.SendAsync(atribuir);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // 4) POST .../diagnostico
        using (var diagnostico = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/diagnostico", "founder"))
        {
            diagnostico.Content = JsonContent.Create(new { diagnostico = "Pasta térmica ressecada." });
            using var resposta = await _fixture.Client.SendAsync(diagnostico);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("EmDiagnostico", corpo.GetProperty("status").GetString());
        }

        // 5) POST .../orcamento — 1 peça prevista + mão de obra.
        using (var orcamento = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/orcamento", "founder"))
        {
            orcamento.Content = JsonContent.Create(new
            {
                pecasPrevistas = new[] { new { produtoId = "produto-fonte-1", descricao = "Fonte ADP-400DR", quantidade = 1, precoUnitarioCentavos = 39_000L } },
                maoDeObraCentavos = 12_000L,
                validadeDias = 10
            });
            using var resposta = await _fixture.Client.SendAsync(orcamento);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("AguardandoAprovacao", corpo.GetProperty("status").GetString());
            Assert.Equal(51_000L, corpo.GetProperty("orcamento").GetProperty("total").GetProperty("centavos").GetInt64());
        }

        // 6) POST .../aprovacao
        using (var aprovacao = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/aprovacao", "founder"))
        {
            aprovacao.Content = JsonContent.Create(new { canal = "WhatsApp", registradoPorId = "cliente-1", registradoPorNome = "Pedro Lima" });
            using var resposta = await _fixture.Client.SendAsync(aprovacao);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Aprovada", corpo.GetProperty("status").GetString());
        }

        // 7) POST .../iniciar-execucao
        using (var iniciar = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/iniciar-execucao", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(iniciar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("EmExecucao", corpo.GetProperty("status").GetString());
        }

        // 8) POST .../concluir-execucao — sem aplicar peça (linha não-aplicada libera reserva, sem efeito HTTP visível aqui).
        using (var concluir = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/concluir-execucao", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(concluir);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Pronta", corpo.GetProperty("status").GetString());
        }

        // 9) POST .../entregar — fatura + entrega no mesmo ato.
        using (var entregar = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/entregar", "founder"))
        {
            entregar.Content = JsonContent.Create(new { formaPagamento = "Pix", descontoCentavos = 0L, garantiaDias = 90 });
            using var resposta = await _fixture.Client.SendAsync(entregar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Entregue", corpo.GetProperty("status").GetString());
            Assert.Equal("Pix", corpo.GetProperty("formaPagamento").GetString());
        }

        // 10) GET /assistencia/ordens/{id} — detalhe final reflete o status terminal.
        using (var detalhe = _fixture.Requisicao(HttpMethod.Get, $"/api/assistencia/ordens/{osId}", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(detalhe);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal("Entregue", corpo.GetProperty("status").GetString());
        }

        // 11) Transição inválida pós-terminal (FSM guard) ⇒ 422, nunca 200.
        using (var cancelarPosEntrega = _fixture.Requisicao(HttpMethod.Post, $"/api/assistencia/ordens/{osId}/cancelar", "founder"))
        {
            cancelarPosEntrega.Content = JsonContent.Create(new { motivo = "Tentativa inválida pós-entrega." });
            using var resposta = await _fixture.Client.SendAsync(cancelarPosEntrega);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        }
    }

    [Fact]
    public async Task GetOrdem_Inexistente_Retorna404()
    {
        using var request = _fixture.Requisicao(HttpMethod.Get, "/api/assistencia/ordens/nao-existe", "founder");
        using var resposta = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
