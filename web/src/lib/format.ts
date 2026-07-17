/**
 * Formatadores compartilhados — nunca usar `new Date(valor)` direto num template,
 * sempre passar por `safeDate` primeiro (evita `RangeError` com valor inválido/undefined).
 */

const BRL = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

const BRL_COMPACT = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
  notation: 'compact',
  maximumFractionDigits: 1,
});

export function formatCurrency(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return BRL.format(value);
}

/** Versão sem símbolo de moeda repetido, útil em séries de gráfico no eixo Y. */
export function formatCurrencyCompact(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return BRL_COMPACT.format(value);
}

export function formatSignedCurrency(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  const sign = value > 0 ? '+' : '';
  return `${sign}${BRL.format(value)}`;
}

export function formatPercent(value: number | null | undefined, digits = 0): string {
  if (value === null || value === undefined || Number.isNaN(value)) return '—';
  return `${value.toFixed(digits)}%`;
}

const DATE_ONLY_RE = /^\d{4}-\d{2}-\d{2}$/;

export function safeDate(input: Date | string | number | null | undefined): Date | null {
  if (input === null || input === undefined) return null;
  // `new Date('yyyy-mm-dd')` parses como UTC (spec), não local — desalinha 1 dia
  // em fusos negativos (Brasil, Américas). Forçamos meia-noite local aqui.
  const d = input instanceof Date ? input : new Date(typeof input === 'string' && DATE_ONLY_RE.test(input) ? `${input}T00:00:00` : input);
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatDate(input: Date | string | number | null | undefined): string {
  const d = safeDate(input);
  if (!d) return '-';
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(d);
}

export function formatDateShort(input: Date | string | number | null | undefined): string {
  const d = safeDate(input);
  if (!d) return '-';
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' }).format(d);
}

export function formatWeekday(input: Date | string | number | null | undefined): string {
  const d = safeDate(input);
  if (!d) return '-';
  return new Intl.DateTimeFormat('pt-BR', { weekday: 'short' }).format(d).replace('.', '');
}

export function formatDateTime(input: Date | string | number | null | undefined): string {
  const d = safeDate(input);
  if (!d) return '-';
  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

/** "há 3 dias" / "em 2 dias" / "hoje" — usado nas linhas do feed do Consultor. */
export function formatRelativeDays(input: Date | string | number | null | undefined): string {
  const d = safeDate(input);
  if (!d) return '-';
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const target = new Date(d);
  target.setHours(0, 0, 0, 0);
  const days = Math.round((target.getTime() - today.getTime()) / 86_400_000);
  if (days === 0) return 'hoje';
  if (days === 1) return 'amanhã';
  if (days === -1) return 'ontem';
  if (days > 1) return `em ${days} dias`;
  return `há ${Math.abs(days)} dias`;
}

export function daysBetween(a: Date | string, b: Date | string): number {
  const da = safeDate(a);
  const db = safeDate(b);
  if (!da || !db) return 0;
  return Math.round((db.getTime() - da.getTime()) / 86_400_000);
}
