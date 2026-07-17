using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Vendas.Application.Ports;
using SistemaX.Modules.Vendas.Infrastructure.InMemory;
using SistemaX.Modules.Vendas.Infrastructure.Sqlite;

namespace SistemaX.Modules.Vendas.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Vendas — registra o adapter concreto de
/// <see cref="IVendaRepository"/>, no mesmo espírito de <c>ComprasInfrastructureModule</c>:
/// vive separado da Application porque só a Infrastructure enxerga port e adapter concreto ao
/// mesmo tempo (grafo <c>Infrastructure → Application → Domain</c>, nunca o inverso).
///
/// Quando <c>contexto.Configuracao["persistencia"] == "sqlite"</c>, troca
/// <see cref="InMemoryVendaRepository"/> por <see cref="SqliteVendaRepository"/> e registra a
/// migração de schema correspondente — ZERO mudança em Domain/Application. Default (config
/// ausente, como em todo teste hoje) continua in-memory.
/// </summary>
public sealed class VendasInfrastructureModule : IModule
{
    public string Codigo => "vendas.infra";
    public string Nome => "Vendas — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["vendas"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            // Scoped: acompanha a mesma sessão (ILocalSessao) do caso de uso que a resolveu —
            // registrar Singleton aqui seria uma dependência cativa de um serviço Scoped.
            services.AddScoped<IVendaRepository, SqliteVendaRepository>();
            services.AddModuleSchemaMigration<VendasSchemaMigrationV1>();
            services.AddModuleSchemaMigration<VendasSchemaMigrationV2>();
        }
        else
        {
            services.AddSingleton<IVendaRepository, InMemoryVendaRepository>();
        }
    }
}
