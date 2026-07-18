using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.Ports;
using SistemaX.Modules.Fiscal.Infrastructure.InMemory;
using SistemaX.Modules.Fiscal.Infrastructure.Sefaz;
using SistemaX.Modules.Fiscal.Infrastructure.Sqlite;

namespace SistemaX.Modules.Fiscal.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Fiscal — registra os adapters concretos dos ports, mesmo
/// espírito de <c>FinanceiroInfrastructureModule</c>/<c>EstoqueInfrastructureModule</c>. Correção
/// estrutural fixada em ADR-0002/docs/fiscal/arquitetura.md §6: a primeira versão do design
/// misturava Application e Infrastructure num só <c>IModule</c> — não compila no grafo
/// <c>Infrastructure → Application → Domain</c> do repo.
/// </summary>
public sealed class FiscalInfrastructureModule : IModule
{
    public string Codigo => "fiscal.infra";
    public string Nome => "Fiscal — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["fiscal"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Camada == CamadaExecucao.Nuvem)
        {
            // Cloud nunca aloca número fiscal (docs/fiscal/arquitetura.md §5) — sem
            // ISequenciaFiscalRepository na Nuvem. Repositórios de leitura/consolidação ficam
            // fora de escopo desta fase (gateway/consolidação multi-loja, ver §9).
            return;
        }

        var persistenciaSqlite = contexto.Configuracao["persistencia"] == "sqlite";

        if (persistenciaSqlite)
        {
            services.AddScoped<IConfiguracaoFiscalTenantRepository, SqliteConfiguracaoFiscalTenantRepository>();
            services.AddScoped<IPerfilFiscalNcmRepository, SqlitePerfilFiscalNcmRepository>();
            services.AddScoped<ITributacaoProdutoRepository, SqliteTributacaoProdutoRepository>();
            services.AddScoped<IRegraFiscalPorOperacaoRepository, SqliteRegraFiscalPorOperacaoRepository>();
            services.AddScoped<IRegraCfopRepository, SqliteRegraCfopRepository>();
            services.AddScoped<IDadosFiscaisProdutoCacheRepository, SqliteDadosFiscaisProdutoCacheRepository>();
            services.AddScoped<IDocumentoFiscalRepository, SqliteDocumentoFiscalRepository>();
            services.AddScoped<ISequenciaFiscalRepository, SqliteSequenciaFiscalRepository>();
            services.AddScoped<ICartaCorrecaoFiscalRepository, SqliteCartaCorrecaoFiscalRepository>();
            // Delega para ILocalSessao (já Scoped por AddSistemaXLocalInfrastructure) — fecha o
            // gap de atomicidade número+documento descrito em EmitirDocumentoFiscalUseCase
            // (docs/fiscal/arquitetura.md §5/§7).
            services.AddScoped<IUnidadeDeTrabalhoFiscal, UnidadeDeTrabalhoFiscalSqlite>();
            services.AddModuleSchemaMigration<FiscalSchemaMigrationV1>();
            services.AddModuleSchemaMigration<FiscalSchemaMigrationV2>();
            services.AddModuleSchemaMigration<FiscalSchemaMigrationV3>();
        }
        else
        {
            // Default (config ausente, todo teste hoje) — mesma convenção de Estoque/Vendas.
            services.AddSingleton<IConfiguracaoFiscalTenantRepository, InMemoryConfiguracaoFiscalTenantRepository>();
            services.AddSingleton<IPerfilFiscalNcmRepository, InMemoryPerfilFiscalNcmRepository>();
            services.AddSingleton<ITributacaoProdutoRepository, InMemoryTributacaoProdutoRepository>();
            services.AddSingleton<IRegraFiscalPorOperacaoRepository, InMemoryRegraFiscalPorOperacaoRepository>();
            services.AddSingleton<IRegraCfopRepository, InMemoryRegraCfopRepository>();
            services.AddSingleton<IDadosFiscaisProdutoCacheRepository, InMemoryDadosFiscaisProdutoCacheRepository>();
            services.AddSingleton<IDocumentoFiscalRepository, InMemoryDocumentoFiscalRepository>();
            services.AddSingleton<ISequenciaFiscalRepository, InMemorySequenciaFiscalRepository>();
            services.AddSingleton<ICartaCorrecaoFiscalRepository, InMemoryCartaCorrecaoFiscalRepository>();
            services.AddSingleton<IUnidadeDeTrabalhoFiscal, UnidadeDeTrabalhoFiscalEmMemoria>();
        }

        RegistrarGatewayDeEmissao(services, contexto, persistenciaSqlite);
    }

    /// <summary>
    /// Gateway de emissão (docs/fiscal/emissao-mapping.md §2) — SEMPRE registrado (independe de
    /// "persistencia"), config via env vars <c>SEFAZ_API_URL</c>/<c>SEFAZ_API_KEY</c>/
    /// <c>SEFAZ_AMBIENTE</c> (mesmo trio do saas-erp; a API key pertence ao <c>.env</c> da
    /// instalação, NUNCA commitada). Os repositórios de insumo (emitente/certificado/
    /// destinatário/pagamento/referência de devolução — gaps #1-#5) seguem a MESMA convenção
    /// sqlite-vs-InMemory dos demais repos do módulo (seed manual via Settings em memória até um
    /// módulo de Cadastro/Empresa/Cofre existir, mesmo papel de
    /// <see cref="InMemoryPerfilFiscalNcmRepository"/>).
    /// </summary>
    private static void RegistrarGatewayDeEmissao(IServiceCollection services, IModuleContext contexto, bool persistenciaSqlite)
    {
        services.AddOptions<SefazGatewayOptions>().Configure(opcoes =>
        {
            opcoes.BaseUrl = contexto.Configuracao["SEFAZ_API_URL"] ?? opcoes.BaseUrl;
            opcoes.ApiKey = contexto.Configuracao["SEFAZ_API_KEY"] ?? opcoes.ApiKey;
            opcoes.Ambiente = contexto.Configuracao["SEFAZ_AMBIENTE"] ?? opcoes.Ambiente;
            // Só relevante quando Ambiente=mock (item 1 das pendências, docs/fiscal/emissao-mapping.md §7.1)
            // — QA forçar o caminho de rejeição/denegação em mock sem precisar do HTTP real.
            opcoes.MockDesfecho = contexto.Configuracao["SEFAZ_MOCK_DESFECHO"] ?? opcoes.MockDesfecho;
        });

        if (persistenciaSqlite)
        {
            services.AddScoped<ICadastroFiscalEmitenteRepository, SqliteCadastroFiscalEmitenteRepository>();
            services.AddScoped<ICertificadoDigitalRepository, SqliteCertificadoDigitalRepository>();
            services.AddScoped<IDestinatarioDocumentoFiscalRepository, SqliteDestinatarioDocumentoFiscalRepository>();
            services.AddScoped<IFormaPagamentoDocumentoFiscalRepository, SqliteFormaPagamentoDocumentoFiscalRepository>();
            services.AddScoped<IReferenciaDevolucaoDocumentoFiscalRepository, SqliteReferenciaDevolucaoDocumentoFiscalRepository>();
        }
        else
        {
            services.AddSingleton<ICadastroFiscalEmitenteRepository, InMemoryCadastroFiscalEmitenteRepository>();
            services.AddSingleton<ICertificadoDigitalRepository, InMemoryCertificadoDigitalRepository>();
            services.AddSingleton<IDestinatarioDocumentoFiscalRepository, InMemoryDestinatarioDocumentoFiscalRepository>();
            services.AddSingleton<IFormaPagamentoDocumentoFiscalRepository, InMemoryFormaPagamentoDocumentoFiscalRepository>();
            services.AddSingleton<IReferenciaDevolucaoDocumentoFiscalRepository, InMemoryReferenciaDevolucaoDocumentoFiscalRepository>();
        }

        services.AddHttpClient<SefazApiGateway>((sp, client) =>
        {
            var opcoes = sp.GetRequiredService<IOptions<SefazGatewayOptions>>().Value;
            client.BaseAddress = new Uri(opcoes.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opcoes.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opcoes.ApiKey);
        });

        services.AddScoped<IGatewayEmissaoSefaz>(sp => sp.GetRequiredService<SefazApiGateway>());
        services.AddScoped<IGatewayCancelamentoSefaz>(sp => sp.GetRequiredService<SefazApiGateway>());
        services.AddScoped<IGatewayInutilizacaoSefaz>(sp => sp.GetRequiredService<SefazApiGateway>());
        services.AddScoped<IGatewayCartaCorrecaoSefaz>(sp => sp.GetRequiredService<SefazApiGateway>());
    }
}
