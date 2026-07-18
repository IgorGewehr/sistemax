using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Smoke ponta-a-ponta HTTP (rotas de PRODUÇÃO, mesmo <see cref="PermissaoHttpFixture"/> de
/// <see cref="RequerPermissaoHttpTests"/>) da Parte B de Análise por Projeto (P3 AtivoDeCapital,
/// P4 ApontamentoDeTempo, P5 Consultor): liga o toggle, cria projeto + ativo (cenário DigiSat de
/// docs/financeiro/design-analise-por-projeto.md §4.3/§9.5 — 5×R$1.379,00 = R$6.895,00, vida 36m),
/// aponta tempo e bate no Painel/Consultor de verdade — prova que o pipeline
/// DI+roteamento+serialização dos endpoints novos funciona rodando a aplicação, não só a lógica
/// isolada em unit test.
/// </summary>
public sealed class AnalisePorProjetoHttpSmokeTests : IClassFixture<PermissaoHttpFixture>
{
    private readonly PermissaoHttpFixture _fixture;

    public AnalisePorProjetoHttpSmokeTests(PermissaoHttpFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task FluxoCompleto_ToggleAtivoProjetoAtivoApontamento_PainelEConsultorRespondemComNumerosReais()
    {
        // 1) Liga o toggle — sem isso, criar Projeto/AtivoDeCapital devolve 422 (§2.2 do design).
        using (var ligarToggle = _fixture.Requisicao(HttpMethod.Put, "/api/financeiro/configuracoes", "founder"))
        {
            ligarToggle.Content = JsonContent.Create(new { analisePorProjetoAtiva = true, custoHoraPadraoCentavos = (long?)null, tempoEntraNoDre = false });
            using var resposta = await _fixture.Client.SendAsync(ligarToggle);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // 2) Cria o projeto.
        string projetoId;
        using (var criarProjeto = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/projetos", "founder"))
        {
            criarProjeto.Content = JsonContent.Create(new { nome = "DigiSat Smoke" });
            using var resposta = await _fixture.Client.SendAsync(criarProjeto);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            projetoId = corpo.GetProperty("id").GetString()!;
        }

        // 3) Cria o AtivoDeCapital — cenário DigiSat: R$6.895,00 (689.500 centavos), 5 licenças,
        //    vida 36 meses, intangível/licença de software (design §4.3: custo/licença Hamilton =
        //    R$1.379,00 = 689.500 ÷ 5, coberto por AtivoDeCapitalQuantTests.CustoPorLicenca_*).
        using (var criarAtivo = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/ativos", "founder"))
        {
            criarAtivo.Content = JsonContent.Create(new
            {
                nome = "Licenças DigiSat 5x36m",
                natureza = "Intangivel",
                categoria = "LicencaSoftware",
                custoAquisicaoCentavos = 689_500L,
                dataAquisicao = "2026-07-01",
                vidaUtilMeses = 36,
                quantidadeUnidades = 5,
                projetoId
            });
            using var resposta = await _fixture.Client.SendAsync(criarAtivo);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(689_500L, corpo.GetProperty("custoAquisicaoCentavos").GetInt64());
            Assert.Equal(5, corpo.GetProperty("quantidadeUnidades").GetInt32());
            // Amortização mensal cai no cronograma Hamilton (19.153 ou 19.152 — nunca outro valor).
            var amortizacaoMensal = corpo.GetProperty("amortizacaoMensalCentavos").GetInt64();
            Assert.True(amortizacaoMensal is 19_153 or 19_152, $"amortizacaoMensalCentavos inesperado: {amortizacaoMensal}");
        }

        // 4) GET /financeiro/ativos?projetoId= — lista real, filtrada.
        using (var listarAtivos = _fixture.Requisicao(HttpMethod.Get, $"/api/financeiro/ativos?projetoId={projetoId}", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(listarAtivos);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(1, corpo.GetArrayLength());
        }

        // 5) Aponta tempo no projeto (P4) — só minutos, custo sempre null nesta fatia.
        using (var apontar = _fixture.Requisicao(HttpMethod.Post, "/api/financeiro/apontamentos", "founder"))
        {
            apontar.Content = JsonContent.Create(new
            {
                minutos = 90,
                data = DateTimeOffset.UtcNow,
                operadorId = "op-1",
                operadorNome = "Operador Smoke",
                projetoId
            });
            using var resposta = await _fixture.Client.SendAsync(apontar);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);
            Assert.Equal(90, corpo.GetProperty("minutos").GetInt32());
            Assert.False(corpo.TryGetProperty("custoCentavos", out var custo) && custo.ValueKind != JsonValueKind.Null);
        }

        // 6) GET /financeiro/tempo/resumo — cross-projeto/cliente ("onde vai meu tempo").
        using (var resumoTempo = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/tempo/resumo", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(resumoTempo);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        // 7) Painel do Projeto — o entregável central da Parte B (MC2, capacidade/ociosidade,
        //    payback, ROI, bloco de tempo) respondendo de ponta a ponta via HTTP real.
        using (var painel = _fixture.Requisicao(HttpMethod.Get, $"/api/financeiro/projetos/{projetoId}/painel", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(painel);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await LerJsonAsync(resposta);

            var capacidade = corpo.GetProperty("capacidade");
            Assert.Equal(5, capacidade.GetProperty("unidadesTotais").GetInt32());
            // Nenhuma assinatura vinculada neste smoke ⇒ 0 unidades usadas, capacidade 100% ociosa —
            // exatamente o insight do design (§9.6): a amortização corre sobre o total mesmo assim.
            Assert.Equal(0, capacidade.GetProperty("unidadesUtilizadas").GetInt32());
            Assert.True(capacidade.GetProperty("custoOciosidadeMesCentavos").GetInt64() > 0);

            var margem = corpo.GetProperty("margem");
            var amortizacaoMes = margem.GetProperty("amortizacaoMes").GetProperty("centavos").GetInt64();
            Assert.True(amortizacaoMes is 19_153 or 19_152);

            var tempo = corpo.GetProperty("tempo");
            Assert.Equal(90, tempo.GetProperty("minutosJanela").GetInt32());
        }

        // 8) Consultor — fatos fail-quiet da Parte B (payback projetado / custo de ociosidade)
        //    só aparecem com o toggle ligado; aqui só provamos que a rota real não quebra.
        using (var consultor = _fixture.Requisicao(HttpMethod.Get, "/api/financeiro/consultor", "founder"))
        {
            using var resposta = await _fixture.Client.SendAsync(consultor);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }
}
