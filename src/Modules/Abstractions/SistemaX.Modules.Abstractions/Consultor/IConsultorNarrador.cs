namespace SistemaX.Modules.Abstractions.Consultor;

/// <summary>
/// SEAM do Super Consultor (docs/financeiro/inteligencia-arquitetura.md §3.5/ADR-0005, Lei 2 — "LLM
/// é redator, não analista"): recebe os top-N <see cref="ConsultorFato"/> já rankeados e devolve a
/// narrativa final. Assinatura em LOTE (não 1 fato por chamada) porque é assim que o narrador real
/// custeia barato — o plano descreve "1 chamada gpt-4o-mini batch" para todos os insights do dia
/// (~R$0,11/mês nominal), nunca 1 chamada por fato.
///
/// Implementação DETERMINÍSTICA desta rodada: <see cref="NarradorTemplate"/> — devolve
/// <see cref="ConsultorFato.TemplateFallback"/> verbatim, custo zero, sempre disponível (é o "piso"
/// do plano). Um <c>NarradorLLM</c> futuro (Fase 2/3) implementa a MESMA porta: chama
/// <c>Cloud.Api /api/consultor/narrate</c>, valida a frase (cada valor de <c>Facts</c> precisa
/// aparecer literalmente na saída — mesma <c>isValidConsultorPhrase</c> do
/// <c>saas-erp/app/api/financial/consultor/route.ts</c>) e cai de volta pro
/// <see cref="ConsultorFato.TemplateFallback"/> em qualquer falha (sem rede, budget estourado,
/// validação reprovada) — nunca vira erro 500 na UI.
/// </summary>
public interface IConsultorNarrador
{
    Task<IReadOnlyList<ConsultorInsightNarrado>> NarrarAsync(
        IReadOnlyList<ConsultorFato> fatos, CancellationToken ct = default);
}
