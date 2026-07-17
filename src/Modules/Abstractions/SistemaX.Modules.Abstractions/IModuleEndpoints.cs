using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Contrato OPCIONAL que um <see cref="IModule"/> pode implementar para expor endpoints HTTP sob
/// <c>/api/*</c>. É a peça que fecha o Bridge (ver docs/arquitetura — F1a): o Host NUNCA conhece
/// rota concreta de módulo nenhum, só enumera <c>registry.ModulosAdicionados.OfType&lt;IModuleEndpoints&gt;()</c>
/// e chama <see cref="MapearEndpoints"/> uma vez por módulo — mesma regra de ouro do <see cref="IModule"/>.
///
/// Convenção: implemente esta interface junto com <see cref="IModule"/> num módulo dedicado
/// (ex.: <c>FinanceiroEndpointsModule</c>, espelhando <c>FinanceiroInfrastructureModule</c>) que
/// vive na pasta <c>Endpoints/</c> do projeto Application do módulo — nunca em Domain. O
/// <c>Registrar</c> desse módulo tipicamente não registra nada (os serviços que os handlers usam
/// já foram registrados pelo módulo de Application "de verdade"); ele só existe para carregar o
/// <see cref="MapearEndpoints"/>.
/// </summary>
public interface IModuleEndpoints
{
    /// <summary>
    /// Mapeia as rotas HTTP deste módulo dentro do grupo <c>/api</c> já protegido por sessão
    /// Bearer (ver <c>BearerSessionMiddleware</c> no Host) — um handler só roda autenticado.
    /// </summary>
    void MapearEndpoints(IEndpointRouteBuilder api);
}

/// <summary>
/// Acesso ao <c>businessId</c> resolvido pela sessão Bearer do request atual. R1 do projeto:
/// todo endpoint de módulo lê o tenant DAQUI — nunca de query string/corpo do request, que o
/// cliente poderia forjar para ler/escrever dado de outro tenant.
/// </summary>
public static class SessaoHttpContextExtensions
{
    /// <summary>Chave interna do <see cref="HttpContext.Items"/> onde o middleware de sessão do
    /// Host grava o <c>businessId</c> da sessão validada.</summary>
    public const string BusinessIdItemKey = "sistemax.session.business_id";

    /// <summary>Chave interna do <see cref="HttpContext.Items"/> onde o middleware de sessão do
    /// Host grava o papel da sessão validada — uma string reconhecida por
    /// <c>Autorizacao.Papel</c> (<c>founder</c>/<c>admin</c>/<c>manager</c>/<c>operator</c>/
    /// <c>viewer</c>, case-insensitive). Consumida por
    /// <c>Autorizacao.PermissaoEndpointExtensions.RequerPermissao</c> — todo endpoint que declara
    /// uma permissão lê o papel DAQUI, nunca de query/corpo.</summary>
    public const string PapelItemKey = "sistemax.session.papel";

    /// <summary>
    /// O <c>businessId</c> da sessão autenticada do request atual. Lança se chamado num endpoint
    /// que não passou pelo middleware de sessão — sinal de rota mal-registrada (fora do grupo
    /// <c>/api</c> protegido), erro de programação a ser corrigido, não tratado em runtime.
    /// </summary>
    public static string ObterBusinessId(this HttpContext http)
        => http.Items[BusinessIdItemKey] as string
           ?? throw new InvalidOperationException(
               "BusinessId não encontrado em HttpContext.Items — este endpoint está fora do " +
               "middleware de sessão Bearer, ou foi chamado antes dele rodar.");
}
