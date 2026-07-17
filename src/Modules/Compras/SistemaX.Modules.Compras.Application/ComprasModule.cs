using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Compras.Application.CasosDeUso;

namespace SistemaX.Modules.Compras.Application;

/// <summary>
/// Módulo Compras — emissor de <c>CompraRecebida</c>/<c>CompraItensRecebidos</c>/<c>CompraEstornada</c>
/// (Financeiro e Estoque assinam; Compras não conhece nenhum dos dois). Registra os casos de uso
/// (Domain + Application). NÃO registra os adapters concretos de repositório — isso é
/// responsabilidade de <c>ComprasInfrastructureModule</c>, mesmo motivo documentado em
/// <c>VendasModule</c>/<c>FinanceiroModule</c>: o grafo de referência de projeto é
/// <c>Infrastructure → Application → Domain</c>, nunca o inverso.
/// </summary>
public sealed class ComprasModule : IModule
{
    public string Codigo => "compras";
    public string Nome => "Compras";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<CadastrarFornecedorUseCase>();
        services.AddScoped<GerenciarFornecedorUseCase>();
        services.AddScoped<RegistrarEntradaDeNotaUseCase>();
        services.AddScoped<ResolverMatchDeItemUseCase>();
        services.AddScoped<IgnorarItemDaNotaUseCase>();
        services.AddScoped<ConfirmarRecebimentoUseCase>();
        services.AddScoped<EstornarRecebimentoUseCase>();
        services.AddScoped<DescartarNotaUseCase>();
    }
}
