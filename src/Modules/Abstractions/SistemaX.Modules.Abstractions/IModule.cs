using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Contrato de um MÓDULO plugável — base (Financeiro, Vendas, Estoque…) ou vertical
/// (Assistência, Posto, Mercado…). REGRA DE OURO da arquitetura:
///
///   O Core NUNCA conhece um módulo concreto. Ele descobre todos os IModule via DI e fala
///   só com este contrato. Nada de `if (vertical == "posto")` em lugar nenhum.
///
/// Habilitar um vertical numa instalação = registrar seu IModule. Desabilitar = não registrar.
/// Um módulo desligado não carrega absolutamente nada — zero superfície de falha para os
/// demais. É isto que garante "adicionar Posto não quebra o que já está pronto".
/// </summary>
public interface IModule
{
    /// <summary>Código estável e único (ex.: "financeiro", "vendas", "assistencia", "posto").</summary>
    string Codigo { get; }

    string Nome { get; }

    /// <summary>Códigos dos módulos dos quais este depende. O Core valida o grafo no boot.</summary>
    IReadOnlyCollection<string> DependeDe => Array.Empty<string>();

    /// <summary>
    /// Registra no container: serviços, handlers de eventos de integração que o módulo
    /// assina, endpoints, e migrações de schema (fatia local + nuvem) do módulo.
    /// </summary>
    void Registrar(IServiceCollection services, IModuleContext contexto);
}

/// <summary>Contexto injetado no registro do módulo.</summary>
public interface IModuleContext
{
    /// <summary>Em qual das 3 camadas este processo roda (muda o que o módulo liga).</summary>
    CamadaExecucao Camada { get; }

    IConfiguration Configuracao { get; }
}

/// <summary>
/// As 3 camadas onde o MESMO Core.Domain roda. O módulo pode se registrar diferente em cada
/// uma (ex.: hardware só no PDV; consolidação multi-loja só na Nuvem).
/// </summary>
public enum CamadaExecucao
{
    /// <summary>Terminal de caixa/balcão. Offline-first, dono do hardware.</summary>
    Pdv,

    /// <summary>Servidor da loja na LAN — fonte da verdade local, sobrevive a queda de internet.</summary>
    ServidorDeLoja,

    /// <summary>Nuvem — consolidação multi-loja, BI, multi-tenant.</summary>
    Nuvem
}
