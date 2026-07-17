using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Verticals.Assistencia.Application.CasosDeUso;

namespace SistemaX.Verticals.Assistencia.Application;

/// <summary>
/// Módulo do vertical Assistência Técnica (Ordem de Serviço) — P0-2 fechado
/// (docs/financeiro/revisao-domain-fit-cnpj.md): até aqui <c>Registrar</c> não existia e nada
/// publicava <c>OsFaturada</c>/os 4 eventos de peça — fechar uma OS não gerava um único centavo no
/// Financeiro. Registra os casos de uso do agregado <see cref="OrdemDeServico"/> (o Domain já
/// estava pronto — ver <c>docs/arquitetura/COMO-CRIAR-UM-VERTICAL.md</c>), no mesmo espírito de
/// <c>VendasModule</c>: só serviços de Application aqui, o adapter concreto de
/// <c>IOrdemDeServicoRepository</c> é responsabilidade de
/// <c>SistemaX.Verticals.Assistencia.Infrastructure.AssistenciaInfrastructureModule</c> (grafo
/// <c>Infrastructure → Application → Domain</c>, nunca o inverso).
/// </summary>
public sealed class AssistenciaModule : IModule
{
    public string Codigo => "assistencia";
    public string Nome => "Assistência Técnica (Ordem de Serviço)";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<AbrirOsUseCase>();
        services.AddScoped<GerenciarOrdemDeServicoUseCase>();
        services.AddScoped<OrdemDeServicoFaturamentoUseCases>();
    }
}
