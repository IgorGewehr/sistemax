using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Consultor;
using SistemaX.Modules.Estoque.Application;
using SistemaX.Modules.Estoque.Application.Endpoints;
using SistemaX.Modules.Estoque.Infrastructure;
using SistemaX.Modules.Financeiro.Application;
using SistemaX.Modules.Financeiro.Application.Endpoints;
using SistemaX.Modules.Financeiro.Infrastructure;
using SistemaX.Modules.Vendas.Application;
using SistemaX.Modules.Vendas.Application.Endpoints;
using SistemaX.Modules.Vendas.Infrastructure;

namespace SistemaX.Modules.Abstractions.Tests.Autorizacao;

/// <summary>
/// Host HTTP mínimo que prova, contra as rotas de PRODUÇÃO (os mesmos <c>*EndpointsModule</c> que
/// <c>SistemaXHost.RegistrarModulos</c> pluga no Bridge de verdade), que <c>.RequerPermissao(...)</c>
/// está de fato APLICADO. Não recria o Host.Desktop inteiro (que abriria janela Photino, tocaria
/// disco em <c>config.json</c>/SQLite e é demais para um teste de unidade) — só o pedaço relevante:
/// <see cref="ModuleRegistry"/> com persistência in-memory (config vazia ⇒ default dos módulos,
/// mesma regra que todo teste hoje já usa) + roteamento minimal-API sob <c>/api</c>.
///
/// A ÚNICA substituição em relação à produção é o próprio <c>BearerSessionMiddleware</c>: aqui um
/// middleware de uma linha lê o cabeçalho <c>X-Test-Papel</c> e grava direto em
/// <c>HttpContext.Items</c> — exatamente o mesmo contrato
/// (<see cref="SessaoHttpContextExtensions.PapelItemKey"/>/<c>BusinessIdItemKey</c>) que o
/// middleware real grava depois de validar a sessão Bearer. Trocar SÓ essa peça (autenticação) é
/// o que permite testar autorização isoladamente, sem precisar emitir token/PIN de verdade.
/// </summary>
public sealed class PermissaoHttpFixture : IDisposable
{
    public const string BusinessIdDeTeste = "tenant-teste";
    private const string CabecalhoPapel = "X-Test-Papel";

    public TestServer Server { get; }
    public HttpClient Client { get; }

    public PermissaoHttpFixture()
    {
        var hostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                var contexto = new ContextoDeTeste(new ConfigurationBuilder().Build());

                var registry = new ModuleRegistry()
                    .Adicionar(new EstoqueModule())
                    .Adicionar(new EstoqueInfrastructureModule())
                    .Adicionar(new EstoqueEndpointsModule())
                    .Adicionar(new VendasModule())
                    .Adicionar(new VendasInfrastructureModule())
                    .Adicionar(new VendasEndpointsModule())
                    .Adicionar(new FinanceiroModule())
                    .Adicionar(new FinanceiroInfrastructureModule())
                    .Adicionar(new FinanceiroEndpointsModule());

                registry.RegistrarTodos(services, contexto);
                services.AddSingleton(registry);
                services.AddRouting();

                // Mesmo registro module-agnostic de SistemaXHost.RegistrarModulos (ver comentário
                // lá) — sem isso, `GET /api/financeiro/consultor` não resolve `ConsultorService`
                // via DI e o minimal-API infere errado a origem do parâmetro (tenta tratar como
                // Body), quebrando a construção de TODAS as rotas deste TestServer.
                services.AddSingleton<IConsultorInsightCache, InMemoryConsultorInsightCache>();
                services.AddScoped<IConsultorNarrador, NarradorTemplate>();
                services.AddScoped<ConsultorService>();
            })
            .Configure(app =>
            {
                // Substitui SÓ o BearerSessionMiddleware (autenticação) — ver doc da classe.
                app.Use(async (context, next) =>
                {
                    context.Items[SessaoHttpContextExtensions.BusinessIdItemKey] = BusinessIdDeTeste;
                    if (context.Request.Headers.TryGetValue(CabecalhoPapel, out var papel))
                        context.Items[SessaoHttpContextExtensions.PapelItemKey] = papel.ToString();

                    await next(context);
                });

                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    var api = endpoints.MapGroup("/api");
                    var registry = endpoints.ServiceProvider.GetRequiredService<ModuleRegistry>();
                    foreach (var modulo in registry.ModulosAdicionados.OfType<IModuleEndpoints>())
                        modulo.MapearEndpoints(api);
                });
            });

        Server = new TestServer(hostBuilder);
        Client = Server.CreateClient();
    }

    /// <summary>Monta um request com o papel dado no cabeçalho que o middleware de teste lê —
    /// <c>null</c> simula uma sessão cujo papel não foi reconhecido (branch defensivo do filtro).</summary>
    public HttpRequestMessage Requisicao(HttpMethod metodo, string caminho, string? papel)
    {
        var request = new HttpRequestMessage(metodo, caminho);
        if (papel is not null) request.Headers.Add(CabecalhoPapel, papel);
        return request;
    }

    public void Dispose()
    {
        Client.Dispose();
        Server.Dispose();
    }

    private sealed class ContextoDeTeste(IConfiguration configuracao) : IModuleContext
    {
        public CamadaExecucao Camada => CamadaExecucao.Pdv;
        public IConfiguration Configuracao => configuracao;
    }
}
