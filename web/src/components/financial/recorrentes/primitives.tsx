/**
 * Primitivos de UI reusados pelos dois painéis (Fixas/Assinaturas) desta tela —
 * lista de barras clicável (`bar-click`), tiles de estatística (`.stat`), badge de
 * delta com seta e o botão "voltar" do drill (`.back`). Vocabulário local à tela;
 * ver `docs/ui/financeiro-ui.md` §5 para o vocabulário compartilhado do Financeiro.
 */
import { ArrowDownRight, ArrowUpRight, ChevronLeft } from 'lucide-react';
import type { ReactNode } from 'react';

import type { Centavos } from '@/lib/money';
import { cn } from '@/lib/utils';

import { formatCentavosWhole, formatSignedCentavosWhole } from './calc';

// ───────────────────────── Valor monetário (reais inteiros, mockup: `brl()`) ─────────────────────────

interface MoneyTextProps {
  centavos: Centavos | null | undefined;
  /** Mostra sinal explícito (+/−) — usado em deltas/diferenças. */
  signed?: boolean;
  className?: string;
}

/**
 * Equivalente local ao `MoneyValue` compartilhado, mas em reais inteiros (mockup desta tela
 * nunca mostra centavos — ver `formatCentavosWhole`). Vive aqui em vez de mudar o componente
 * compartilhado, que outras telas usam com 2 casas decimais.
 */
export function MoneyText({ centavos, signed = false, className }: MoneyTextProps) {
  return <span className={cn('num', className)}>{signed ? formatSignedCentavosWhole(centavos) : formatCentavosWhole(centavos)}</span>;
}

// ───────────────────────── Lista de barras clicável (índice ⇄ drill) ─────────────────────────

export interface RankedBarRow {
  id: string;
  name: ReactNode;
  amount: ReactNode;
  pct: number;
  pctLabel: string;
  /** Classe Tailwind do preenchimento da barra (ex. `bg-warn`, `bg-foreground/40`). */
  barClassName: string;
}

interface RankedBarListProps {
  rows: RankedBarRow[];
  onSelect: (id: string) => void;
  /** Lista rolável — usada quando há muitos itens (mockup: `.bars.scroll`). */
  scrollable?: boolean;
}

/** Ranking clicável com barra de participação — mockup: `.bar-click`. */
export function RankedBarList({ rows, onSelect, scrollable }: RankedBarListProps) {
  return (
    <div className={cn('flex flex-col gap-0.5 p-3', scrollable && 'max-h-[322px] overflow-y-auto')}>
      {rows.map((row) => (
        <button
          key={row.id}
          type="button"
          onClick={() => onSelect(row.id)}
          className="flex w-full flex-col gap-1.5 rounded-xl px-2.5 py-2.5 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring active:brightness-95"
        >
          <span className="flex items-center justify-between gap-2.5">
            <span className="text-[13px] font-semibold text-foreground">{row.name}</span>
            <span className="num shrink-0 text-[12.5px] text-muted-foreground">{row.amount}</span>
          </span>
          <span className="block h-2 w-full overflow-hidden rounded-full bg-surface-2">
            <span className={cn('block h-full rounded-full', row.barClassName)} style={{ width: `${Math.min(100, row.pct)}%` }} />
          </span>
          <span className="num self-end text-[12.5px] font-bold text-foreground">{row.pctLabel}</span>
        </button>
      ))}
    </div>
  );
}

// ───────────────────────── Tiles de estatística ─────────────────────────

interface StatTileProps {
  k: ReactNode;
  v: ReactNode;
  s: ReactNode;
}

/** Tile de estatística (mockup: `.stat`) — usado em "Retrato do fixo", drills e retenção. */
export function StatTile({ k, v, s }: StatTileProps) {
  return (
    <div className="rounded-xl bg-surface-2 px-3.5 py-3">
      <div className="text-xs font-semibold text-muted-foreground">{k}</div>
      <div className="num mt-1 text-2xl font-bold tracking-tight text-foreground">{v}</div>
      <div className="mt-0.5 text-xs text-faint">{s}</div>
    </div>
  );
}

export function StatTiles({ children }: { children: ReactNode }) {
  return <div className="flex flex-col gap-2.5 px-4 pb-4 sm:px-[18px] sm:pb-[18px]">{children}</div>;
}

// ───────────────────────── Delta de KPI (seta + texto) ─────────────────────────

type DeltaTone = 'pos' | 'crit' | 'neutral';

interface DeltaRowProps {
  tone: DeltaTone;
  icon?: 'up' | 'down' | 'none';
  children: ReactNode;
}

/** Linha de delta de KPI com seta semântica — cor SEMPRE reservada a estado (pos/crit). */
export function DeltaRow({ tone, icon = 'none', children }: DeltaRowProps) {
  const toneClass = tone === 'pos' ? 'text-pos' : tone === 'crit' ? 'text-crit' : 'text-muted-foreground';
  const Icon = icon === 'up' ? ArrowUpRight : icon === 'down' ? ArrowDownRight : null;
  return (
    <div className={cn('mt-[7px] inline-flex items-center gap-1 text-[12.5px] font-semibold', toneClass)}>
      {Icon && <Icon className="h-[13px] w-[13px]" strokeWidth={2.5} />}
      {children}
    </div>
  );
}

// ───────────────────────── Header de drill (voltar + título) ─────────────────────────

interface DrillHeaderTitleProps {
  onBack: () => void;
  backLabel: string;
  children: ReactNode;
}

/** Título do card em modo drill: botão "←" + nome do item selecionado (mockup: `.secleft`). */
export function DrillHeaderTitle({ onBack, backLabel, children }: DrillHeaderTitleProps) {
  return (
    <span className="inline-flex items-center gap-2">
      <button
        type="button"
        onClick={onBack}
        aria-label={backLabel}
        className="grid h-[26px] w-[26px] flex-none place-items-center rounded-lg bg-surface-2 text-foreground transition-colors hover:bg-primary-soft hover:text-primary-600 active:brightness-95"
      >
        <ChevronLeft className="h-4 w-4" />
      </button>
      {children}
    </span>
  );
}
