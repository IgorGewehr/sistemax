using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SistemaX.Host.Desktop.Bridge;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;

namespace SistemaX.Host.Desktop.Tests;

/// <summary>
/// Teste de PONTA-A-PONTA do ADR-0003 §5: sobe o pipeline HTTP REAL do Bridge (
/// <see cref="BearerSessionMiddleware"/> + <see cref="BridgeEndpoints"/> + o filtro real
/// <c>RequerPermissao</c>) num <see cref="TestServer"/> em memória, com usuários reais de papéis
/// distintos. Prova que login por PIN identifica a PESSOA certa e que o RBAC do servidor passa a
/// valer de verdade — não só ficar autenticado, mas AUTORIZADO (ou barrado) conforme o papel.
/// </summary>
public sealed class BridgeAuthEndToEndTests
{
    private const string BusinessId = "loja-teste";
    private const string BootToken = "boot-token-de-teste";

    private const string PinFounder = "1111";
    private const string PinManager = "2222";
    private const string PinViewer = "3333";

    [Fact]
    public async Task Login_com_pin_invalido_falha_com_401()
    {
        await using var ambiente = await SubirServidorAsync();

        var resposta = await LoginAsync(ambiente.Client, "0000");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_pin_de_manager_autentica_com_papel_manager_e_200_numa_rota_que_ele_pode()
    {
        await using var ambiente = await SubirServidorAsync();

        var (statusLogin, corpoLogin) = await LoginEDecodificarAsync(ambiente.Client, PinManager);
        Assert.Equal(HttpStatusCode.OK, statusLogin);
        Assert.Equal("manager", corpoLogin.GetProperty("papel").GetString());

        var token = corpoLogin.GetProperty("token").GetString();

        // Manager TEM Estoque:Editar por padrão (PermissoesPadraoPorPapel) — deve passar.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/estoque/editar-teste");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var resposta = await ambiente.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_pin_de_viewer_autentica_com_papel_viewer_e_403_numa_rota_de_edicao()
    {
        await using var ambiente = await SubirServidorAsync();

        var (statusLogin, corpoLogin) = await LoginEDecodificarAsync(ambiente.Client, PinViewer);
        Assert.Equal(HttpStatusCode.OK, statusLogin);
        Assert.Equal("viewer", corpoLogin.GetProperty("papel").GetString());

        var token = corpoLogin.GetProperty("token").GetString();

        // Viewer NÃO tem Financeiro:Editar (nem Financeiro:Ver) — prova ponta-a-ponta de que o
        // RBAC do servidor agora vale: sessão autenticada, mas AUTORIZAÇÃO barra a ação.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/financeiro/editar-teste");
        request.Headers.Add("Authorization", $"Bearer {token}");
        var resposta = await ambiente.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Usuario_desativado_apos_login_perde_acesso_imediatamente_mesmo_com_token_ainda_valido()
    {
        await using var ambiente = await SubirServidorAsync();

        var (_, corpoLogin) = await LoginEDecodificarAsync(ambiente.Client, PinManager);
        var token = corpoLogin.GetProperty("token").GetString();

        // Confirma que o token funciona antes de desativar.
        using (var antes = new HttpRequestMessage(HttpMethod.Get, "/api/estoque/editar-teste"))
        {
            antes.Headers.Add("Authorization", $"Bearer {token}");
            Assert.Equal(HttpStatusCode.OK, (await ambiente.Client.SendAsync(antes)).StatusCode);
        }

        var manager = (await ambiente.Repositorio.ListarAsync(BusinessId, incluirInativos: true))
            .Single(u => u.Papel == Papel.Manager);
        manager.Desativar();
        await ambiente.Repositorio.SalvarAsync(manager);

        using var depois = new HttpRequestMessage(HttpMethod.Get, "/api/estoque/editar-teste");
        depois.Headers.Add("Authorization", $"Bearer {token}");
        var resposta = await ambiente.Client.SendAsync(depois);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_do_founder_recem_semeado_com_pin_provisorio_retorna_deve_trocar_pin_true()
    {
        await using var ambiente = await SubirServidorAsync(founderComPinProvisorio: true);

        var (status, corpo) = await LoginEDecodificarAsync(ambiente.Client, PinFounder);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(corpo.GetProperty("deveTrocarPin").GetBoolean());
    }

    [Fact]
    public async Task Login_de_usuario_sem_pin_provisorio_retorna_deve_trocar_pin_false()
    {
        await using var ambiente = await SubirServidorAsync();

        var (status, corpo) = await LoginEDecodificarAsync(ambiente.Client, PinFounder);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(corpo.GetProperty("deveTrocarPin").GetBoolean());
    }

    [Fact]
    public async Task Trocar_o_proprio_pin_zera_pin_provisorio_e_o_proximo_login_ja_nao_pede_troca()
    {
        await using var ambiente = await SubirServidorAsync(founderComPinProvisorio: true);

        var (_, corpoLogin) = await LoginEDecodificarAsync(ambiente.Client, PinFounder);
        Assert.True(corpoLogin.GetProperty("deveTrocarPin").GetBoolean());
        var token = corpoLogin.GetProperty("token").GetString();

        using var trocaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/trocar-pin")
        {
            Content = JsonContent.Create(new { pinAtual = PinFounder, pinNovo = "7391" })
        };
        trocaRequest.Headers.Add("Authorization", $"Bearer {token}");
        var respostaTroca = await ambiente.Client.SendAsync(trocaRequest);
        Assert.Equal(HttpStatusCode.OK, respostaTroca.StatusCode);

        var (statusNovoLogin, corpoNovoLogin) = await LoginEDecodificarAsync(ambiente.Client, "7391");
        Assert.Equal(HttpStatusCode.OK, statusNovoLogin);
        Assert.False(corpoNovoLogin.GetProperty("deveTrocarPin").GetBoolean());

        // O PIN antigo (1234) não bate mais — RedefinirPin invalida o hash anterior.
        var respostaComPinAntigo = await LoginAsync(ambiente.Client, PinFounder);
        Assert.Equal(HttpStatusCode.Unauthorized, respostaComPinAntigo.StatusCode);
    }

    [Fact]
    public async Task Trocar_o_proprio_pin_com_pin_atual_errado_falha_com_422_e_nao_altera_o_pin()
    {
        await using var ambiente = await SubirServidorAsync(founderComPinProvisorio: true);

        var (_, corpoLogin) = await LoginEDecodificarAsync(ambiente.Client, PinFounder);
        var token = corpoLogin.GetProperty("token").GetString();

        using var trocaRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/trocar-pin")
        {
            Content = JsonContent.Create(new { pinAtual = "0000", pinNovo = "5678" })
        };
        trocaRequest.Headers.Add("Authorization", $"Bearer {token}");
        var respostaTroca = await ambiente.Client.SendAsync(trocaRequest);

        // PIN atual errado é 422 (validação), NÃO 401 — senão o interceptador global de 401 do
        // cliente web descarta a sessão e chuta o usuário pro login no meio do wizard de troca.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, respostaTroca.StatusCode);

        // PIN original ainda funciona e a instalação ainda pede troca.
        var (statusLoginAntigo, corpoLoginAntigo) = await LoginEDecodificarAsync(ambiente.Client, PinFounder);
        Assert.Equal(HttpStatusCode.OK, statusLoginAntigo);
        Assert.True(corpoLoginAntigo.GetProperty("deveTrocarPin").GetBoolean());
    }

    [Fact]
    public async Task Trocar_pin_sem_sessao_falha_com_401()
    {
        await using var ambiente = await SubirServidorAsync(founderComPinProvisorio: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/trocar-pin")
        {
            Content = JsonContent.Create(new { pinAtual = PinFounder, pinNovo = "5678" })
        };
        var resposta = await ambiente.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string pin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { pin })
        };
        request.Headers.Add("X-Boot-Token", BootToken);
        return await client.SendAsync(request);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Corpo)> LoginEDecodificarAsync(HttpClient client, string pin)
    {
        var resposta = await LoginAsync(client, pin);
        var texto = await resposta.Content.ReadAsStringAsync();
        return (resposta.StatusCode, JsonDocument.Parse(texto).RootElement.Clone());
    }

    private static async Task<AmbienteDeTeste> SubirServidorAsync(bool founderComPinProvisorio = false)
    {
        var repositorio = new InMemoryUsuarioRepository();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<SessionStore>();
                    services.AddSingleton<IUsuarioRepository>(repositorio);
                    services.AddScoped<AutenticarPorPinUseCase>();
                    services.AddScoped<TrocarPinUseCase>();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<BearerSessionMiddleware>();
                    app.UseEndpoints(endpoints =>
                    {
                        var api = endpoints.MapGroup("/api");

                        var hostConfig = new HostConfig(
                            InstalacaoId: "teste",
                            BusinessId: BusinessId,
                            NomeLoja: "Loja de Teste",
                            Porta: 0,
                            LogLevel: "Information",
                            Persistencia: "memoria",
                            PinAdminHash: string.Empty,
                            PinAdminSalt: string.Empty,
                            UiUrl: null);

                        BridgeEndpoints.Mapear(
                            api, hostConfig, BootToken,
                            app.ApplicationServices.GetRequiredService<SessionStore>(),
                            DateTimeOffset.UtcNow);

                        // Rotas de teste protegidas por permissões REAIS — prova end-to-end sem
                        // precisar registrar um módulo de domínio inteiro (Financeiro/Estoque) só
                        // pra exercitar o filtro RequerPermissao.
                        api.MapGet("/financeiro/editar-teste", () => Results.Ok(new { ok = true }))
                            .RequerPermissao(Modulo.Financeiro, Acao.Editar);
                        api.MapGet("/estoque/editar-teste", () => Results.Ok(new { ok = true }))
                            .RequerPermissao(Modulo.Estoque, Acao.Editar);
                    });
                });
            })
            .StartAsync();

        var founder = Usuario.Criar(
            BusinessId, "Founder", "founder@teste.com", PinFounder, Papel.Founder,
            pinProvisorio: founderComPinProvisorio).Valor;
        var manager = Usuario.Criar(BusinessId, "Gerente", "gerente@teste.com", PinManager, Papel.Manager).Valor;
        var viewer = Usuario.Criar(BusinessId, "Visualizador", "viewer@teste.com", PinViewer, Papel.Viewer).Valor;
        await repositorio.SalvarAsync(founder);
        await repositorio.SalvarAsync(manager);
        await repositorio.SalvarAsync(viewer);

        return new AmbienteDeTeste(host, host.GetTestClient(), repositorio);
    }

    private sealed record AmbienteDeTeste(IHost Host, HttpClient Client, IUsuarioRepository Repositorio) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Host.StopAsync();
            Host.Dispose();
        }
    }
}
