using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Estoque.Application.CasosDeUso;
using SistemaX.Modules.Estoque.Application.EventosDeIntegracao.Handlers;
using SistemaX.Modules.Estoque.Application.ReadModels;

namespace SistemaX.Modules.Estoque.Application;

/// <summary>
/// Módulo Estoque — o SEGUNDO assinante-mestre do sistema (espelho do Financeiro): nunca é
/// chamado, só assina eventos de integração e mantém saldo como consequência. Registra os
/// handlers dos eventos que o Estoque ASSINA (venda/compra por item + os 4 promovidos da OS), os
/// casos de uso manuais e os read-models de análise.
///
/// Mesma decisão de design do <c>FinanceiroModule</c>/<c>VendasModule</c>: os adapters concretos
/// dos ports (repositórios in-memory) NÃO são registrados aqui — isso é
/// <see cref="Infrastructure.EstoqueInfrastructureModule"/> (grafo
/// <c>Infrastructure → Application → Domain</c>, nunca o inverso).
/// </summary>
public sealed class EstoqueModule : IModule
{
    public string Codigo => "estoque";
    public string Nome => "Estoque";

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        services.AddScoped<IIntegrationEventHandler<VendaItensMovimentados>, VendaItensMovimentadosHandler>();
        services.AddScoped<IIntegrationEventHandler<VendaEstornada>, VendaEstornadaHandler>();
        services.AddScoped<IIntegrationEventHandler<CompraItensRecebidos>, CompraItensRecebidosHandler>();
        services.AddScoped<IIntegrationEventHandler<CompraEstornada>, CompraEstornadaHandler>();
        services.AddScoped<IIntegrationEventHandler<PecaReservada>, PecaReservadaHandler>();
        services.AddScoped<IIntegrationEventHandler<PecaConsumida>, PecaConsumidaHandler>();
        services.AddScoped<IIntegrationEventHandler<ReservaLiberada>, ReservaLiberadaHandler>();
        services.AddScoped<IIntegrationEventHandler<ConsumoEstornado>, ConsumoEstornadoHandler>();

        services.AddScoped<CriarProdutoUseCase>();
        services.AddScoped<RegistrarEntradaManualUseCase>();
        services.AddScoped<RegistrarPerdaUseCase>();
        services.AddScoped<RecalcularSaldoUseCase>();
        services.AddScoped<AtualizarDadosFiscaisProdutoUseCase>();

        services.AddScoped<SaldoAtualService>();
        services.AddScoped<CurvaAbcService>();
        services.AddScoped<GiroDeEstoqueService>();
        services.AddScoped<RupturaService>();
    }
}
