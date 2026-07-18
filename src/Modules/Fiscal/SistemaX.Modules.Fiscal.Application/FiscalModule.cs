using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Fiscal.Application.CasosDeUso;
using SistemaX.Modules.Fiscal.Application.Cfop;
using SistemaX.Modules.Fiscal.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Fiscal.Application.Motor;
using SistemaX.Modules.Fiscal.Application.Ports;

namespace SistemaX.Modules.Fiscal.Application;

/// <summary>
/// Módulo Fiscal (Application) — handlers dos eventos de integração que o Fiscal ASSINA, os
/// casos de uso e os serviços de resolução (nenhum adapter concreto de porta aqui — isso é
/// <see cref="Infrastructure.FiscalInfrastructureModule"/>, grafo
/// <c>Infrastructure → Application → Domain</c>, mesma decisão de todo módulo do repo, ver
/// docs/fiscal/arquitetura.md §6).
///
/// SEM <c>DependeDe</c> em "estoque"/"vendas": assinar um evento de integração de outro módulo
/// não exige esse módulo fisicamente presente na instalação — só o TIPO do evento, que vive em
/// <c>Modules.Abstractions</c> (kernel compartilhado que todo módulo já referencia). Mesmo padrão
/// que <c>EstoqueModule</c> já demonstra hoje ao assinar eventos de Vendas/Compras/OS sem
/// declarar essa dependência.
/// </summary>
public sealed class FiscalModule : IModule
{
    public string Codigo => "fiscal";
    public string Nome => "Fiscal";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<IIntegrationEventHandler<VendaItensMovimentados>, VendaItensMovimentadosHandler>();
        services.AddScoped<IIntegrationEventHandler<ProdutoFiscalAtualizado>, ProdutoFiscalAtualizadoHandler>();
        services.AddScoped<IIntegrationEventHandler<ProdutoFiscalAtualizadoEmLote>, ProdutoFiscalAtualizadoEmLoteHandler>();

        services.AddScoped<IResolvedorDeCfop, ResolvedorDeCfop>();
        services.AddScoped<ResolvedorDeItemFiscalService>();

        services.AddScoped<TransmitirDocumentoFiscalUseCase>();
        services.AddScoped<EmitirDocumentoFiscalUseCase>();
        services.AddScoped<RetransmitirDocumentosPendentesUseCase>();
        services.AddScoped<CancelarDocumentoFiscalUseCase>();
        services.AddScoped<DesistirDeNumeroUseCase>();
        services.AddScoped<EmitirCartaCorrecaoUseCase>();
    }
}
