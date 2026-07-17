using Microsoft.Extensions.Configuration;
using SistemaX.Modules.Abstractions;

namespace SistemaX.Host.Desktop.Composition;

/// <summary>Contexto de registro entregue a cada módulo — em qual das 3 camadas este processo roda
/// e a configuração da instalação (quais verticais o cliente comprou, endpoints, etc.).</summary>
public sealed class ModuleContext(CamadaExecucao camada, IConfiguration configuracao) : IModuleContext
{
    public CamadaExecucao Camada => camada;
    public IConfiguration Configuracao => configuracao;
}
