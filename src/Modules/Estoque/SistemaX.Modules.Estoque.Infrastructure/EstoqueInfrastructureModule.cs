using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.Ports;
using SistemaX.Modules.Estoque.Infrastructure.InMemory;
using SistemaX.Modules.Estoque.Infrastructure.Sqlite;

namespace SistemaX.Modules.Estoque.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Estoque — registra os adapters concretos dos ports, no mesmo
/// espírito de <c>FinanceiroInfrastructureModule</c>/<c>VendasInfrastructureModule</c>.
///
/// Quando <c>contexto.Configuracao["persistencia"] == "sqlite"</c>, troca os 3 adapters in-memory
/// pelos equivalentes SQLite (ver docs/persistencia/persistencia-sqlite.md) e registra as
/// migrações de schema correspondentes — ZERO mudança em Domain/Application. Default (config
/// ausente, como em todo teste hoje) continua in-memory.
/// </summary>
public sealed class EstoqueInfrastructureModule : IModule
{
    public string Codigo => "estoque.infra";
    public string Nome => "Estoque — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["estoque"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            services.AddScoped<IProdutoRepository, SqliteProdutoRepository>();
            services.AddModuleSchemaMigration<EstoqueSchemaMigrationV1>();
            services.AddModuleSchemaMigration<EstoqueSchemaMigrationV4>();

            services.AddScoped<ISaldoRepository, SqliteSaldoRepository>();
            services.AddModuleSchemaMigration<EstoqueSchemaMigrationV2>();

            services.AddScoped<IMovimentoRepository, SqliteMovimentoRepository>();
            services.AddModuleSchemaMigration<EstoqueSchemaMigrationV3>();
        }
        else
        {
            services.AddSingleton<IProdutoRepository, InMemoryProdutoRepository>();
            services.AddSingleton<IMovimentoRepository, InMemoryMovimentoRepository>();
            services.AddSingleton<ISaldoRepository, InMemorySaldoRepository>();
        }
    }
}
