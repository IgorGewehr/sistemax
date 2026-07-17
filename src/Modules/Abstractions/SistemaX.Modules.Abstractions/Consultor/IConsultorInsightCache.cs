namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// Cache de insight narrado, por (tenant, módulo, rule) + hash dos fatos — implementa o passo 3 do
/// pipeline (ADR-0005 §3.5): "Se sha256(Facts) == hash do último narrado da mesma rule → reusa
/// frase antiga (custo 0)". <see cref="ObterSeAtualAsync"/> só devolve o insight quando o hash bate
/// EXATAMENTE — qualquer mudança nos fatos invalida o cache (nunca serve frase desatualizada).
///
/// Hoje uma implementação simples (<see cref="InMemoryConsultorInsightCache"/>), suficiente para o
/// narrador determinístico grátis desta rodada. Quando o narrador LLM entrar em produção, esta
/// porta ganha um adapter persistente (SQLite local, espelhando <c>consultor_insights</c> do
/// roadmap) sem qualquer mudança no <c>ConsultorService</c>.
/// </summary>
public interface IConsultorInsightCache
{
    Task<ConsultorInsightNarrado?> ObterSeAtualAsync(
        string businessId, string modulo, string ruleId, string factsHash, CancellationToken ct = default);

    Task GravarAsync(
        string businessId, string modulo, string ruleId, string factsHash, ConsultorInsightNarrado insight,
        CancellationToken ct = default);
}
