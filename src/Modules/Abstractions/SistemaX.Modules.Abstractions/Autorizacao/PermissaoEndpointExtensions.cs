using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Abstractions.Autorizacao;

/// <summary>
/// Mecanismo de ENFORCEMENT — o que faltava na auditoria (achado ALTA): <see cref="BusinessIdItemKey"/>
/// e <see cref="SessaoHttpContextExtensions.PapelItemKey"/> já eram gravados por
/// <c>BearerSessionMiddleware</c>, mas nenhum endpoint lia o papel para decidir nada. <c>.RequerPermissao(...)</c>
/// é a chamada que cada rota de módulo faz para declarar QUAL módulo+ação ela exige — sem essa
/// chamada, a rota continua só autenticada (qualquer papel passa), nunca autorizada.
///
/// Uso (dentro de <c>MapearEndpoints</c> de um <c>*EndpointsModule</c>):
/// <code>
/// api.MapPost("/vendas", (...) => ...).RequerPermissao(Modulo.Vendas, Acao.Editar);
/// api.MapGet("/estoque/saldos", (...) => ...).RequerPermissao(Modulo.Estoque, Acao.Ver);
/// </code>
/// </summary>
public static class PermissaoEndpointExtensions
{
    /// <summary>
    /// Exige que o papel da sessão (gravado em <c>HttpContext.Items</c> pelo
    /// <c>BearerSessionMiddleware</c>) tenha a permissão <paramref name="modulo"/>:<paramref name="acao"/>
    /// — devolve 403 <c>auth.sem_permissao</c> se não tiver, ou 403 <c>auth.papel_desconhecido</c>
    /// se a sessão não carrega um papel reconhecido (não deveria acontecer com o middleware atual,
    /// mas falha fechado — nunca deixa passar por dúvida). Roda DEPOIS do
    /// <c>BearerSessionMiddleware</c> (que já garante 401 para sessão ausente/inválida) — este
    /// filtro só decide AUTORIZAÇÃO, nunca AUTENTICAÇÃO.
    /// </summary>
    public static TBuilder RequerPermissao<TBuilder>(this TBuilder builder, Modulo modulo, Acao acao)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (contexto, proximo) =>
        {
            var http = contexto.HttpContext;
            var papelBruto = http.Items[SessaoHttpContextExtensions.PapelItemKey] as string;

            if (string.IsNullOrWhiteSpace(papelBruto) || !Enum.TryParse<Papel>(papelBruto, ignoreCase: true, out var papel))
            {
                return new Error(
                    "auth.papel_desconhecido",
                    $"Sessão sem papel reconhecido ('{papelBruto}') — acesso negado.")
                    .ParaRespostaHttp(StatusCodes.Status403Forbidden);
            }

            if (!PermissoesPadraoPorPapel.Tem(papel, modulo, acao))
            {
                return new Error(
                    "auth.sem_permissao",
                    $"Papel '{papel}' não tem a permissão '{new Permissao(modulo, acao)}'.")
                    .ParaRespostaHttp(StatusCodes.Status403Forbidden);
            }

            return await proximo(contexto).ConfigureAwait(false);
        });

        return builder;
    }
}
