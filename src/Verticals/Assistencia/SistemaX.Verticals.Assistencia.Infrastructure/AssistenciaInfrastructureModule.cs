using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Verticals.Assistencia.Application.Ports;
using SistemaX.Verticals.Assistencia.Infrastructure.InMemory;

namespace SistemaX.Verticals.Assistencia.Infrastructure;

/// <summary>
/// Segundo <see cref="IModule"/> do vertical Assistência — registra o adapter concreto de
/// <see cref="IOrdemDeServicoRepository"/>, no mesmo espírito de <c>VendasInfrastructureModule</c>.
/// Default in-memory (nenhum host desta rodada passa <c>persistencia=sqlite</c> para este
/// vertical ainda — quando precisar sobreviver a restart, trocar por um adapter SQLite aqui é
/// ZERO mudança em Domain/Application, mesmo gesto já demonstrado pelos módulos core).
/// </summary>
public sealed class AssistenciaInfrastructureModule : IModule
{
    public string Codigo => "assistencia.infra";
    public string Nome => "Assistência Técnica — Infraestrutura";
    public IReadOnlyCollection<string> DependeDe => ["assistencia"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddSingleton<IOrdemDeServicoRepository, InMemoryOrdemDeServicoRepository>();
    }
}
