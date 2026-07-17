import { formatCurrency } from './format';

/**
 * Dinheiro na UI é **SEMPRE centavos inteiros** — espelha o `Money` (long de centavos) do domínio
 * .NET. Regra dura do sistema: dinheiro nunca é float de reais (erro de arredondamento é
 * inaceitável num sistema cujo coração é o financeiro). Converte-se para reais só na EXIBIÇÃO.
 */
export type Centavos = number;

/** Açúcar p/ autoria de mocks/exemplos: `reais(200)` → 20000 centavos. Legível e seguro. */
export const reais = (valorEmReais: number): Centavos => Math.round(valorEmReais * 100);

/** Centavos → "R$ 1.234,56" (ou "—" p/ nulo). Só exibição. */
export function formatCentavos(centavos: Centavos | null | undefined): string {
  if (centavos === null || centavos === undefined || Number.isNaN(centavos)) return '—';
  return formatCurrency(centavos / 100);
}

/** Centavos com sinal explícito: "+R$ 12,00" / "−R$ 42,00". Usa minus unicode p/ tipografia. */
export function formatSignedCentavos(centavos: Centavos | null | undefined): string {
  if (centavos === null || centavos === undefined || Number.isNaN(centavos)) return '—';
  const sinal = centavos > 0 ? '+' : centavos < 0 ? '−' : '';
  return `${sinal}${formatCurrency(Math.abs(centavos) / 100)}`;
}

// Reais INTEIROS (sem casas decimais). Vários mockups do Financeiro (visao-geral, entradas-saidas,
// recorrentes, bancario, fluxo-de-caixa) exibem dinheiro assim — o helper `brl()`/`money()` deles
// faz `Math.round` e nunca mostra centavos. `formatCentavos` (com 2 casas) continua sendo o padrão
// onde a precisão importa (margem "R$ 0,18", diferença média). Fonte única — não duplicar por tela.
const BRL_WHOLE = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

/** Centavos → "R$ 3.300" (reais inteiros, sem decimais) — igual ao `brl()`/`money()` dos mockups. */
export function formatCentavosWhole(centavos: Centavos | null | undefined): string {
  if (centavos === null || centavos === undefined || Number.isNaN(centavos)) return '—';
  return BRL_WHOLE.format(Math.round(centavos / 100));
}

/** Reais inteiros com sinal explícito: "+R$ 650" / "−R$ 4.900". */
export function formatSignedCentavosWhole(centavos: Centavos | null | undefined): string {
  if (centavos === null || centavos === undefined || Number.isNaN(centavos)) return '—';
  const sinal = centavos > 0 ? '+' : centavos < 0 ? '−' : '';
  return `${sinal}${BRL_WHOLE.format(Math.round(Math.abs(centavos) / 100))}`;
}
