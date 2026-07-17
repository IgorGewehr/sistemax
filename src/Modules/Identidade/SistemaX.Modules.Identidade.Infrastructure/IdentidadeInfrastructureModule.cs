using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Infrastructure.InMemory;
using SistemaX.Modules.Identidade.Infrastructure.Sqlite;

namespace SistemaX.Modules.Identidade.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Identidade — registra o adapter concreto do port, no mesmo
/// espírito de <c>ComprasInfrastructureModule</c>: quando
/// <c>contexto.Configuracao["persistencia"] == "sqlite"</c>, troca
/// <see cref="InMemoryUsuarioRepository"/> por <see cref="SqliteUsuarioRepository"/> e registra a
/// migração de schema correspondente — ZERO mudança em Domain/Application.
/// </summary>
public sealed class IdentidadeInfrastructureModule : IModule
{
    public string Codigo => "identidade.infra";
    public string Nome => "Identidade — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["identidade"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            // Scoped: acompanha a mesma sessão (ILocalSessao) do caso de uso que a resolveu.
            services.AddScoped<IUsuarioRepository, SqliteUsuarioRepository>();
            services.AddModuleSchemaMigration<IdentidadeSchemaMigrationV1>();
        }
        else
        {
            services.AddSingleton<IUsuarioRepository, InMemoryUsuarioRepository>();
        }
    }
}
