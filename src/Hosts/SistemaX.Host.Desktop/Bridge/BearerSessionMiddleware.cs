using System.Text.Json;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Host.Desktop.Bridge;

/// <summary>
/// Exige <c>Authorization: Bearer {token}</c> em toda rota <c>/api/*</c>, exceto
/// <see cref="RotasAnonimas"/> (health/login). Sessão válida → resolve o <c>Usuario</c> FRESCO do
/// <see cref="IUsuarioRepository"/> (ADR-0003 §3: papel/status nunca ficam cacheados no token) e
/// grava <c>businessId</c>/<c>papel</c> em <see cref="HttpContext.Items"/> (ver
/// <see cref="SessaoHttpContextExtensions"/>) para todo endpoint de módulo ler — R1 do projeto:
/// o tenant NUNCA vem de query string/corpo do request, só da sessão validada aqui. Usuário
/// desativado (ou apagado) desde a emissão do token → sessão é revogada na hora e o request cai
/// com 401, mesmo com o TTL do token ainda válido — é assim que "desativei o funcionário" tem
/// efeito imediato.
///
/// Também aplica <c>Cache-Control: no-store</c> em toda resposta de <c>/api/*</c> (§2.3 do plano
/// de produção — o bridge é 100% local/sem CDN, nada deveria cachear estas respostas).
/// </summary>
public sealed class BearerSessionMiddleware(RequestDelegate next, SessionStore sessoes)
{
    private static readonly string[] RotasAnonimas = ["/api/health", "/api/auth/login"];

    // Mesma convenção camelCase dos DTOs de módulo (ASP.NET Core configura isso por padrão pros
    // Results.Ok/Results.Json dos endpoints minimal-API — aqui escrevemos a resposta na mão, então
    // replicamos a opção pra não vazar PascalCase inconsistente no wire).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context, IUsuarioRepository usuarios)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";

        if (RotasAnonimas.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var cabecalho = context.Request.Headers.Authorization.ToString();
        if (!cabecalho.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await EscreverErroAsync(context, StatusCodes.Status401Unauthorized,
                "auth.sem_sessao", "Requisição sem token de sessão (Authorization: Bearer ...).");
            return;
        }

        var token = cabecalho["Bearer ".Length..].Trim();
        var sessao = sessoes.Validar(token);
        if (sessao is null)
        {
            await EscreverErroAsync(context, StatusCodes.Status401Unauthorized,
                "auth.sessao_invalida", "Sessão inválida ou expirada — faça login novamente.");
            return;
        }

        var usuario = await usuarios.ObterPorIdAsync(sessao.UsuarioId, context.RequestAborted).ConfigureAwait(false);
        if (usuario is null || usuario.Status != StatusUsuario.Ativo)
        {
            sessoes.Revogar(token);
            await EscreverErroAsync(context, StatusCodes.Status401Unauthorized,
                "auth.usuario_inativo", "Usuário desativado ou removido — faça login novamente.");
            return;
        }

        context.Items[SessaoHttpContextExtensions.BusinessIdItemKey] = sessao.BusinessId;
        context.Items[SessaoHttpContextExtensions.PapelItemKey] = usuario.Papel.ToString();

        await next(context).ConfigureAwait(false);
    }

    private static Task EscreverErroAsync(HttpContext context, int statusCode, string codigo, string mensagem)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new ErroResponse(codigo, mensagem), JsonOptions));
    }
}
