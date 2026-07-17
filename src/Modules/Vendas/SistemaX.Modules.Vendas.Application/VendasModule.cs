using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Vendas.Application.CasosDeUso;

namespace SistemaX.Modules.Vendas.Application;

/// <summary>
/// Módulo Vendas — o exemplo EMISSOR de "tudo alimenta o financeiro" (ARCHITECTURE.md §5).
/// Registra os casos de uso do PDV (Domain + Application). NÃO registra o adapter concreto de
/// <c>IVendaRepository</c> — isso é responsabilidade de <c>VendasInfrastructureModule</c>, pelo
/// mesmo motivo documentado em <c>FinanceiroModule</c>: o grafo de referência de projeto é
/// <c>Infrastructure → Application → Domain</c>, nunca o inverso.
/// </summary>
public sealed class VendasModule : IModule
{
    public string Codigo => "vendas";
    public string Nome => "Vendas";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<IniciarVendaUseCase>();
        services.AddScoped<MontarVendaUseCase>();
        services.AddScoped<ConcluirVendaUseCase>();
        services.AddScoped<EstornarVendaUseCase>();
    }
}
