import type { AccountOption, AgingBucket, DocFormat, DocGenState } from './types';

/** Chave do mapa de estado de geração — um botão PDF/Excel independente por card. */
export function docGenKey(cardId: string, format: DocFormat): string {
  return `${cardId}:${format}`;
}

export function getDocGenState(map: Record<string, DocGenState>, cardId: string, format: DocFormat): DocGenState {
  return map[docGenKey(cardId, format)] ?? 'idle';
}

/**
 * Larguras (%) da barra de aging — derivadas dos valores das faixas, nunca hardcoded, pra nunca
 * divergir do total exibido no flag "atrasado" (replica visualmente o mockup sem duplicar números).
 */
export function agingWidths(buckets: AgingBucket[]): number[] {
  const total = buckets.reduce((sum, b) => sum + b.amountCentavos, 0);
  if (total <= 0) return buckets.map(() => 0);
  return buckets.map((b) => (b.amountCentavos / total) * 100);
}

/**
 * Réplica exata da lógica `toggleAcct` do mockup: "Todas" é exclusivo; ao desmarcar a última conta
 * específica selecionada, a seleção volta pra "Todas" sozinha.
 */
export function toggleAccountSelection(selected: string[], clickedId: string, allId = 'todas'): string[] {
  if (clickedId === allId) return [allId];
  const withoutAll = selected.filter((id) => id !== allId);
  const next = withoutAll.includes(clickedId)
    ? withoutAll.filter((id) => id !== clickedId)
    : [...withoutAll, clickedId];
  return next.length === 0 ? [allId] : next;
}

/** Réplica de `updateExtratoSummary` do mockup: "todas as contas" ou a lista de rótulos selecionados. */
export function extratoSummaryLabel(selected: string[], accounts: AccountOption[], allId = 'todas'): string {
  if (selected.includes(allId)) return 'Selecionado: todas as contas';
  const labels = accounts.filter((a) => selected.includes(a.id) && a.id !== allId).map((a) => a.label);
  return `Selecionado: ${labels.join(', ')}`;
}
