using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Abstractions.Autorizacao;
using SistemaX.Modules.Identidade.Application.CasosDeUso;
using SistemaX.Modules.Identidade.Application.Ports;
using SistemaX.Modules.Identidade.Domain.Usuarios;

namespace SistemaX.Modules.Identidade.Application.Endpoints;

/// <summary>DTO de fio (wire) do usuário — NUNCA inclui <c>PinHash</c>/<c>PinSalt</c> (vazaria o
/// segredo derivado, mesmo sendo hash — não há motivo pra sair do servidor).</summary>
public sealed record UsuarioDto(
    string Id,
    string Nome,
    string Email,
    string Papel,
    bool Ativo,
    DateTimeOffset CriadoEm,
    DateTimeOffset? UltimoAcessoEm)
{
    public static UsuarioDto DeDominio(Usuario usuario) => new(
        usuario.Id,
        usuario.Nome,
        usuario.Email,
        usuario.Papel.ToString().ToLowerInvariant(),
        usuario.Status == StatusUsuario.Ativo,
        usuario.CriadoEm,
        usuario.UltimoAcessoEm);
}

public sealed record CriarUsuarioRequest(string Nome, string Email, string Pin, string Papel);

public sealed record AlterarUsuarioRequest(string? Papel = null, bool? Ativo = null, string? Pin = null);

/// <summary>
/// Segundo <see cref="IModule"/> do Identidade — existe só para implementar
/// <see cref="IModuleEndpoints"/>, no mesmo espírito de <c>EstoqueEndpointsModule</c>. Todas as
/// rotas exigem <c>Modulo.Configuracoes:Acao.GerenciarUsuarios</c> (ADR-0003, tabela de rotas) —
/// só founder/admin administram usuários por padrão (ver <c>PermissoesPadraoPorPapel</c>).
/// </summary>
public sealed class IdentidadeEndpointsModule : IModule, IModuleEndpoints
{
    public string Codigo => "identidade.endpoints";
    public string Nome => "Identidade — Endpoints HTTP";
    public IReadOnlyCollection<string> DependeDe => ["identidade"];

    public void Registrar(IServiceCollection services, IModuleContext contexto)
    {
        // Sem registro de serviço — só rotas, ver MapearEndpoints.
    }

    public void MapearEndpoints(IEndpointRouteBuilder api)
    {
        // GET /api/usuarios — lista ativos+inativos do tenant da sessão (R1).
        api.MapGet("/usuarios", async (
            HttpContext http,
            IUsuarioRepository repositorio,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var usuarios = await repositorio.ListarAsync(businessId, incluirInativos: true, ct).ConfigureAwait(false);
            return Results.Ok(usuarios.Select(UsuarioDto.DeDominio));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        // POST /api/usuarios — cria usuário novo. BusinessId SEMPRE da sessão (R1) — o corpo
        // nunca carrega tenant.
        api.MapPost("/usuarios", async (
            HttpContext http,
            CriarUsuarioRequest corpo,
            CriarUsuarioUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            if (!Enum.TryParse<Papel>(corpo.Papel, ignoreCase: true, out var papel))
            {
                return Results.Json(
                    new { codigo = "usuario.papel_invalido", mensagem = $"Papel '{corpo.Papel}' desconhecido." },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var resultado = await useCase.ExecutarAsync(
                businessId, corpo.Nome, corpo.Email, corpo.Pin, papel, ct).ConfigureAwait(false);

            if (!resultado.Sucesso)
            {
                var statusCode = resultado.Erro.Codigo == "usuario.pin_duplicado"
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status422UnprocessableEntity;
                return Results.Json(new { codigo = resultado.Erro.Codigo, mensagem = resultado.Erro.Mensagem }, statusCode: statusCode);
            }

            return Results.Ok(UsuarioDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);

        // PATCH /api/usuarios/{id} — altera papel/ativo/PIN. A invariante "founder intocável /
        // nunca ficar sem founder ativo" é aplicada dentro de AlterarUsuarioUseCase.
        api.MapPatch("/usuarios/{id}", async (
            HttpContext http,
            string id,
            AlterarUsuarioRequest corpo,
            AlterarUsuarioUseCase useCase,
            CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();

            Papel? novoPapel = null;
            if (!string.IsNullOrWhiteSpace(corpo.Papel))
            {
                if (!Enum.TryParse<Papel>(corpo.Papel, ignoreCase: true, out var papelParseado))
                {
                    return Results.Json(
                        new { codigo = "usuario.papel_invalido", mensagem = $"Papel '{corpo.Papel}' desconhecido." },
                        statusCode: StatusCodes.Status422UnprocessableEntity);
                }

                novoPapel = papelParseado;
            }

            var resultado = await useCase.ExecutarAsync(
                businessId, id, novoPapel, corpo.Ativo, corpo.Pin, ct).ConfigureAwait(false);

            if (!resultado.Sucesso)
            {
                var statusCode = resultado.Erro.Codigo switch
                {
                    "usuario.nao_encontrado" => StatusCodes.Status404NotFound,
                    "usuario.pin_duplicado" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status422UnprocessableEntity,
                };
                return Results.Json(new { codigo = resultado.Erro.Codigo, mensagem = resultado.Erro.Mensagem }, statusCode: statusCode);
            }

            return Results.Ok(UsuarioDto.DeDominio(resultado.Valor));
        }).RequerPermissao(Modulo.Configuracoes, Acao.GerenciarUsuarios);
    }
}
