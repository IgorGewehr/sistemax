using SistemaX.Modules.Financeiro.Application.Ports;
using SistemaX.SharedKernel;

namespace SistemaX.Modules.Financeiro.Application.Projetos;

/// <summary>
/// O CONTRATO do opt-in (docs/financeiro/design-analise-por-projeto.md §2.2) — um único helper
/// checado por TODO caso de uso que grava um <c>projetoId</c> (próprio ou tagging aditivo em
/// entidade existente), nunca um <c>if</c> espalhado por handler. Ausência de linha em
/// <c>ConfiguracaoFinanceiraTenant</c> = desligado (fallback seguro — tenant novo nasce fechado).
///
/// A regra "422 quando desligado" impede estado fantasma (dado tagueado que ninguém vê e que
/// sobreviveria a religar o toggle) e torna o toggle um CONTRATO, não uma cortina de UI.
/// </summary>
public static class AnalisePorProjetoGuard
{
    public static readonly Error Desativado = new(
        "financeiro.projetos.desativado",
        "Análise por Projeto está desativada para este tenant — ative em Configurações antes de usar projetoId.");

    /// <summary>Barra (Result de falha, 422 no boundary HTTP) qualquer chamada aqui enquanto o
    /// toggle estiver desligado. Chame SEMPRE que o comando carregar um <c>projetoId</c> não-nulo
    /// (escrita nova de <see cref="Domain.Projetos.Projeto"/> em si, ou tagging aditivo em
    /// Assinatura/Recorrencia/ContaAReceber/ContaAPagar).</summary>
    public static async Task<Result> ExigirAtivaAsync(
        string businessId, IConfiguracaoFinanceiraTenantRepository configuracoes, CancellationToken ct = default)
    {
        var configuracao = await configuracoes.ObterAsync(businessId, ct).ConfigureAwait(false);
        var ativa = configuracao?.AnalisePorProjetoAtiva ?? false;
        return ativa ? Result.Ok() : Result.Falhar(Desativado);
    }

    /// <summary>Atalho para o caso comum "só valido se o comando trouxe um projetoId" — chamadas
    /// com <paramref name="projetoId"/> nulo passam direto (o toggle desligado nunca impede o
    /// comportamento de sempre, R1 do design: sem projeto = comportamento de hoje, intacto).</summary>
    public static async Task<Result> ExigirAtivaSeProjetoIdAsync(
        string businessId, string? projetoId, IConfiguracaoFinanceiraTenantRepository configuracoes, CancellationToken ct = default)
        => projetoId is null ? Result.Ok() : await ExigirAtivaAsync(businessId, configuracoes, ct).ConfigureAwait(false);
}
