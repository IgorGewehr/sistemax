/**
 * Tipos do módulo Financeiro — camada de UI, com dados mock (sem backend).
 * Espelha o vocabulário de `docs/financeiro/financeiro-ux.md` e
 * `docs/financeiro/financeiro-datamodel.md`. Quando o motor .NET existir de
 * verdade, estes tipos migram para contratos gerados (OpenAPI/Zod-like) — por
 * ora são a fonte da verdade da UI.
 */

export type HealthBand = 'critico' | 'atencao' | 'estavel' | 'saudavel' | 'otimo';

export interface HealthBandMeta {
  band: HealthBand;
  label: string;
  range: [number, number];
  colorVar: string;
}

export const HEALTH_BANDS: HealthBandMeta[] = [
  { band: 'critico', label: 'Crítico', range: [0, 20], colorVar: '#DC2626' },
  { band: 'atencao', label: 'Atenção', range: [21, 40], colorVar: '#F97316' },
  { band: 'estavel', label: 'Estável', range: [41, 60], colorVar: '#F59E0B' },
  { band: 'saudavel', label: 'Saudável', range: [61, 80], colorVar: '#22C55E' },
  { band: 'otimo', label: 'Ótimo', range: [81, 100], colorVar: '#10B981' },
];

export function getHealthBand(score: number): HealthBandMeta {
  const clamped = Math.max(0, Math.min(100, score));
  return HEALTH_BANDS.find((b) => clamped >= b.range[0] && clamped <= b.range[1]) ?? HEALTH_BANDS[2];
}

export interface HealthScore {
  score: number;
  summary: string;
  ctaLabel: string;
  ctaInsightId: string;
}

export type Trend = 'up' | 'down' | 'flat';

export interface ThreeNumberCard {
  id: string;
  label: string;
  value: number;
  deltaPct: number | null;
  trend: Trend;
  /** Se a alta é positiva para o negócio (nem toda subida é boa: ex. despesa). */
  trendIsGood: boolean;
  sublabel: string;
  tooltip: string;
}

export type DueKind = 'a_pagar' | 'a_receber';

export interface DueItem {
  id: string;
  date: string; // ISO yyyy-mm-dd
  label: string;
  counterparty: string;
  amount: number;
  kind: DueKind;
}

export type InsightSeverity = 'critico' | 'atencao' | 'info';

export interface InsightCta {
  id: string;
  label: string;
  kind: 'primary' | 'secondary';
}

export interface CalculationStep {
  label: string;
  value: string;
  isTotal?: boolean;
}

export interface InsightCard {
  id: string;
  ruleId: string;
  severity: InsightSeverity;
  icon: 'trend-down' | 'trend-up' | 'coins' | 'users' | 'calendar' | 'wallet' | 'repeat' | 'sun' | 'layers' | 'pencil-off';
  title: string;
  body: string;
  timestampLabel: string;
  ctas: InsightCta[];
  calculation: CalculationStep[];
  dismissed?: boolean;
  snoozed?: boolean;
}

export type Scenario = 'otimista' | 'realista' | 'pessimista';

export interface CashFlowEvent {
  id: string;
  date: string;
  label: string;
  amount: number;
  kind: DueKind;
}

export interface CashFlowPoint {
  date: string;
  label: string;
  /** Preenchido só até "hoje" — realizado. */
  realized: number | null;
  /** Preenchido a partir de "hoje" (inclusive) — projeção. */
  projected: number | null;
  event?: CashFlowEvent;
  isToday?: boolean;
}

export interface CashFlowSummary {
  points: CashFlowPoint[];
  criticalPoint: { date: string; label: string; amount: number } | null;
  events: CashFlowEvent[];
}

export interface QuickEntryCategory {
  id: string;
  label: string;
  usageCount: number;
}

export type TransactionKind = 'receita' | 'despesa';
export type TransactionStatus = 'pago' | 'pendente' | 'atrasado' | 'cancelado';

export interface TransactionRow {
  id: string;
  description: string;
  category: string;
  amount: number;
  kind: TransactionKind;
  status: TransactionStatus;
  date: string;
  account: string;
}

export interface GlossaryTerm {
  term: string;
  explanation: string;
}
