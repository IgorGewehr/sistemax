using Microsoft.AspNetCore.Http;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Abstractions;

/// <summary>
/// Traduz um <see cref="Error"/> de domínio/aplicação (<c>Result.Falha</c>) para a resposta HTTP
/// padrão do Bridge — <c>{ codigo, mensagem }</c> em camelCase, o MESMO formato do
/// <c>ErroResponse</c> do Host (ver <c>docs/arquitetura/bridge-http-local.md</c> §2). Vive aqui
/// (não em cada módulo) porque é o primeiro helper compartilhado por 2+ endpoints de ESCRITA
/// (Estoque e Vendas na F1c) — mesmo critério de promoção usado para o resto do kernel
/// compartilhado.
///
/// 422 por padrão: regra de negócio violada (ex.: "nome obrigatório", "venda sem itens"), nunca
/// payload malformado (isso já é 400 do próprio model binding do minimal API antes de chegar aqui).
/// </summary>
public static class ErrorHttpExtensions
{
    public static IResult ParaRespostaHttp(this Error erro, int statusCode = StatusCodes.Status422UnprocessableEntity)
        => Results.Json(new { codigo = erro.Codigo, mensagem = erro.Mensagem }, statusCode: statusCode);
}
