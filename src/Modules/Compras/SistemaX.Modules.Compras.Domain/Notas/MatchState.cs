namespace SistemaX.Modules.Compras.Domain.Notas;

/// <summary>
/// Estado do match de um item de nota contra o catálogo (plano §5 — motor de match em cascata).
/// Nesta entrega, o cascata implementado tem 2 degraus reais: <see cref="Auto"/> (vínculo
/// aprendido — <c>VinculoProdutoFornecedor</c>) e a resolução manual explícita cai em
/// <see cref="Manual"/>. <see cref="Sugerido"/> fica reservado para quando as estratégias de NCM/
/// similaridade de descrição (fase 2 do plano) existirem — já é tratado pelas invariantes de
/// <see cref="NotaDeCompra.ConfirmarRecebimento"/> (bloqueia recebimento, exige confirmação
/// humana) para não quebrar quando a fase 2 chegar.
/// </summary>
public enum MatchState
{
    /// <summary>Resolvido automaticamente por um vínculo já aprendido. Zero interação humana.</summary>
    Auto,

    /// <summary>Sugestão heurística (NCM/similaridade) ainda não confirmada — bloqueia recebimento.</summary>
    Sugerido,

    /// <summary>Humano vinculou explicitamente o item a um produto (ou confirmou uma sugestão).</summary>
    Manual,

    /// <summary>Nenhuma estratégia resolveu — bloqueia recebimento até vincular ou ignorar.</summary>
    SemMatch,

    /// <summary>Item excluído deliberadamente do recebimento (amostra, brinde, item que não vira
    /// estoque) — fica FORA do evento para o Estoque, mas seu valor continua contabilizado no
    /// custo de entrada da nota (§3.3 invariante 1: Σ landed == vNF usa TODOS os itens).</summary>
    Ignorado
}
