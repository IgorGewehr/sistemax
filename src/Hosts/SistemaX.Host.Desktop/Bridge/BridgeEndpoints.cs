using SistemaX.Host.Desktop.Updates;
using SistemaX.Modules.Abstractions;
using SistemaX.Modules.Identidade.Application.CasosDeUso;

namespace SistemaX.Host.Desktop.Bridge;

public sealed record LoginRequest(string Pin);

/// <summary><c>DeveTrocarPin</c> espelha <c>Usuario.PinProvisorio</c> de quem logou (ADR — wizard
/// de 1º-boot): <c>true</c> para o founder recém-semeado com o PIN "1234" default, até que ele (ou
/// alguém com permissão) troque o PIN pela primeira vez — ver <c>TrocarPinUseCase</c>/
/// <c>AlterarUsuarioUseCase</c>, os dois únicos caminhos que zeram a flag.</summary>
public sealed record LoginResponse(string Token, string BusinessId, string Papel, DateTimeOffset ExpiraEm, bool DeveTrocarPin);

/// <summary>Corpo de <c>POST /api/auth/trocar-pin</c> — autoatendimento: o usuário troca o PRÓPRIO
/// PIN provando que conhece o atual, sem precisar de <c>Configuracoes:GerenciarUsuarios</c> (essa
/// permissão só é exigida para trocar o PIN de OUTRO usuário via <c>PATCH /usuarios/{id}</c>).</summary>
public sealed record TrocarPinRequest(string PinAtual, string PinNovo);

/// <summary>Formato de erro do wire (ver plano de produção §2.5): <c>{ codigo, mensagem }</c>,
/// mesmo shape usado pelos 422 de <c>Result.Falha</c> dos endpoints de módulo.</summary>
public sealed record ErroResponse(string Codigo, string Mensagem);

/// <summary>
/// Endpoints do PRÓPRIO Host (bridge) — não pertencem a nenhum módulo de domínio, por isso não
/// usam <c>IModuleEndpoints</c>: <c>/api/health</c> (sonda de vida, anônimo), <c>/api/auth/login</c>
/// (troca boot-token + PIN por sessão Bearer) e <c>/api/auth/trocar-pin</c> (autoatendimento —
/// wizard de 1º-boot). Ver <see cref="BearerSessionMiddleware"/> para a lista de rotas anônimas.
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
            return Results.Ok(new LoginResponse(sessao.Token, sessao.BusinessId, papel, sessao.ExpiraEm, usuario.PinProvisorio));
        });

        // POST /api/auth/trocar-pin — o usuário logado troca o PRÓPRIO PIN. Rota autenticada
        // (fora de RotasAnonimas, passa pelo BearerSessionMiddleware) mas SEM RequerPermissao:
        // qualquer papel troca o próprio PIN, a "autorização" é o pinAtual bater dentro do
        // TrocarPinUseCase — é o caminho que o wizard de 1º-boot usa pra encerrar o
        // Usuario.PinProvisorio do founder semeado com PIN "1234".
        api.MapPost("/auth/trocar-pin", async (HttpContext http, TrocarPinRequest corpo, TrocarPinUseCase trocar, CancellationToken ct) =>
        {
            var businessId = http.ObterBusinessId();
            var usuarioId = http.ObterUsuarioId();

            var resultado = await trocar.ExecutarAsync(businessId, usuarioId, corpo.PinAtual, corpo.PinNovo, ct).ConfigureAwait(false);
            if (!resultado.Sucesso)
            {
                var statusCode = resultado.Erro.Codigo switch
                {
                    "auth.pin_atual_incorreto" => StatusCodes.Status401Unauthorized,
                    "usuario.pin_duplicado" => StatusCodes.Status409Conflict,
                    "usuario.nao_encontrado" => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status422UnprocessableEntity,
                };
                return Results.Json(new ErroResponse(resultado.Erro.Codigo, resultado.Erro.Mensagem), statusCode: statusCode);
            }

            return Results.Ok(new { ok = true });
        });
    }
}
