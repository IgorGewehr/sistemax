using Microsoft.Extensions.DependencyInjection;
using SistemaX.Infrastructure.Local.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Application.Ports;
using SistemaX.Modules.Compras.Infrastructure.InMemory;
using SistemaX.Modules.Compras.Infrastructure.Sqlite;

namespace SistemaX.Modules.Compras.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do Compras — registra os adapters concretos dos ports, no mesmo
/// espírito de <c>VendasInfrastructureModule</c>/<c>EstoqueInfrastructureModule</c>: vive separado
/// da Application porque só a Infrastructure enxerga port e adapter concreto ao mesmo tempo.
///
/// <see cref="IFornecedorRepository"/> é o REPOSITÓRIO-MOLDE da F0: quando
/// <c>contexto.Configuracao["persistencia"] == "sqlite"</c> (ver
/// <c>ARCHITECTURE.md</c>/docs/persistencia), troca <c>InMemoryFornecedorRepository</c> por
/// <see cref="SqliteFornecedorRepository"/> e registra a migração de schema correspondente — ZERO
/// mudança em Domain/Application. Default (config ausente, como em todo teste hoje) continua
/// in-memory. Os outros 2 ports do módulo (NotaDeCompra, VinculoProdutoFornecedor) seguem
/// in-memory até a F1 portá-los pelo mesmo molde.
/// </summary>
public sealed class ComprasInfrastructureModule : IModule
{
    public string Codigo => "compras.infra";
    public string Nome => "Compras — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["compras"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        if (contexto.Configuracao["persistencia"] == "sqlite")
        {
            // Scoped: acompanha a mesma sessão (ILocalSessao) do caso de uso que a resolveu —
            // registrar Singleton aqui seria uma dependência cativa de um serviço Scoped.
            services.AddScoped<IFornecedorRepository, SqliteFornecedorRepository>();
            services.AddModuleSchemaMigration<ComprasSchemaMigrationV1>();
        }
        else
        {
            services.AddSingleton<IFornecedorRepository, InMemoryFornecedorRepository>();
        }

        services.AddSingleton<INotaDeCompraRepository, InMemoryNotaDeCompraRepository>();
        services.AddSingleton<IVinculoProdutoFornecedorRepository, InMemoryVinculoProdutoFornecedorRepository>();
    }
}
