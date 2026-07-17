using SistemaX.Host.Desktop.Updates;
using SistemaX.Modules.Identidade.Application.CasosDeUso;

namespace SistemaX.Host.Desktop.Bridge;

public sealed record LoginRequest(string Pin);

public sealed record LoginResponse(string Token, string BusinessId, string Papel, DateTimeOffset ExpiraEm);

/// <summary>Formato de erro do wire (ver plano de produção §2.5): <c>{ codigo, mensagem }</c>,
/// mesmo shape usado pelos 422 de <c>Result.Falha</c> dos endpoints de módulo.</summary>
public sealed record ErroResponse(string Codigo, string Mensagem);

/// <summary>
/// Endpoints do PRÓPRIO Host (bridge) — não pertencem a nenhum módulo de domínio, por isso não
/// usam <c>IModuleEndpoints</c>: <c>/api/health</c> (sonda de vida, anônimo) e
/// <c>/api/auth/login</c> (troca boot-token + PIN por sessão Bearer). Ver
/// <see cref="BearerSessionMiddleware"/> para a lista de rotas anônimas.
/// </summary>
public static class BridgeEndpoints
{
    public static void Mapear(
        IEndpointRouteBuilder api,
        HostConfig config,
        string bootToken,
        SessionStore sessoes,
        DateTimeOffset iniciadoEm)
    {
        // versao/atualizacaoAutomaticaHabilitada — ADR-0004 item 3: uma fonte única de versão
        // (VersaoAssembly, derivada do mesmo `-p:Version=` do publish) e o estado HONESTO do
        // updater (nunca "true" sem feed configurado de verdade, ver IServicoDeAtualizacao).
        api.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            instalacaoId = config.InstalacaoId,
            businessId = config.BusinessId,
            nomeLoja = config.NomeLoja,
            uptimeSegundos = (long)(DateTimeOffset.UtcNow - iniciadoEm).TotalSeconds,
            versao = VersaoAssembly.Atual,
            atualizacaoAutomaticaHabilitada = !string.IsNullOrWhiteSpace(config.AtualizacaoFeedUrl)
        }));

        // PIN → sessão Bearer. Exige o boot-token (cabeçalho X-Boot-Token) que só quem recebeu a
        // URL de abertura da janela (`/?boot={token}`) — ou leu o log do processo — conhece; é a
        // defesa descrita no plano §2.3: outro processo local não pode chamar a API às cegas só
        // porque sabe a porta.
        //
        // ADR-0003 §2/§5: o PIN não é mais comparado contra um único hash de `config.json` — é
        // verificado contra TODOS os usuários ATIVOS da instalação via AutenticarPorPinUseCase
        // (Identidade.Application, resolvido por DI); o papel da sessão é o `Usuario.Papel` real
        // de quem bateu o PIN, nunca mais "admin" hardcoded.
        api.MapPost("/auth/login", async (HttpContext http, LoginRequest corpo, AutenticarPorPinUseCase autenticar) =>
        {
            if (sessoes.EstaBloqueado(out var restante))
            {
                return Results.Json(
                    new ErroResponse("auth.bloqueado", $"Muitas tentativas — tente novamente em {Math.Ceiling(restante.TotalSeconds)}s."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var bootHeader = http.Request.Headers["X-Boot-Token"].ToString();
            if (!string.Equals(bootHeader, bootToken, StringComparison.Ordinal))
            {
                sessoes.RegistrarTentativaFalha();
                return Results.Json(
                    new ErroResponse("auth.boot_token_invalido", "Boot-token ausente ou inválido — abra o app pela janela oficial."),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var resultado = await autenticar.ExecutarAsync(config.BusinessId, corpo.Pin, http.RequestAborted).ConfigureAwait(false);
            if (!resultado.Sucesso)
            {
                sessoes.RegistrarTentativaFalha();
                return Results.Json(
                    new ErroResponse(resultado.Erro.Codigo, resultado.Erro.Mensagem),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var usuario = resultado.Valor;
            var sessao = sessoes.Criar(config.BusinessId, usuarioId: usuario.Id);
            var papel = usuario.Papel.ToString().ToLowerInvariant();
            return Results.Ok(new LoginResponse(sessao.Token, sessao.BusinessId, papel, sessao.ExpiraEm));
        });
    }
}
