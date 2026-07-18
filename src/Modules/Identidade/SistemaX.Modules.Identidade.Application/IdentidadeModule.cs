using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.CasosDeUso;

namespace SistemaX.Modules.Identidade.Application;

/// <summary>
/// Módulo Identidade (ADR-0003) — usuários reais, papel de verdade por pessoa. Registra os casos
/// de uso (Domain + Application). NÃO registra o adapter concreto de repositório — isso é
/// responsabilidade de <c>IdentidadeInfrastructureModule</c>, mesmo motivo documentado em
/// <c>ComprasModule</c>: o grafo de referência de projeto é
/// <c>Infrastructure → Application → Domain</c>, nunca o inverso.
/// </summary>
public sealed class IdentidadeModule : IModule
{
    public string Codigo => "identidade";
    public string Nome => "Identidade";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<AutenticarPorPinUseCase>();
        services.AddScoped<CriarUsuarioUseCase>();
        services.AddScoped<AlterarUsuarioUseCase>();
        services.AddScoped<TrocarPinUseCase>();
    }
}
